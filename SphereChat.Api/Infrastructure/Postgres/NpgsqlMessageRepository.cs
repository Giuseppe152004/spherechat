using Npgsql;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Chat;

namespace SphereChat.Api.Infrastructure.Postgres;

/// <summary>
/// Adaptador de Persistencia hacia "message"."history" usando Npgsql.
/// Equivalente a SkunkMessageRepository.scala.
/// Mapeo plano sin JSONB, desarmando el ADT MessageContent en columnas opcionales.
/// </summary>
public class NpgsqlMessageRepository : IMessageRepository
{
    private readonly string _connectionString;

    public NpgsqlMessageRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Message> SaveAsync(Message message)
    {
        // Flat Mapping: Descomponemos el ADT en columnas opcionales (igual que Scala)
        string? contentOpt = null, url = null, attType = null, thumb = null, wave = null;
        long? size = null;
        double? dur = null;

        switch (message.Content)
        {
            case TextContent tc:
                contentOpt = tc.Text;
                break;
            case PhotoContent pc:
                contentOpt = pc.Caption; url = pc.Url; attType = "image"; size = pc.SizeBytes;
                break;
            case VideoContent vc:
                url = vc.Url; attType = "video"; size = vc.SizeBytes; dur = vc.DurationSeconds; thumb = vc.Thumb;
                break;
            case AudioContent ac:
                url = ac.Url; attType = "audio"; dur = ac.DurationSeconds; wave = ac.Waveform;
                break;
            case StickerContent sc:
                contentOpt = $"{sc.StickerId}|{(sc.IsAnimated ? "1" : "0")}"; attType = "sticker";
                break;
            case DocumentContent dc:
                contentOpt = $"{dc.FileName}.{dc.FileExtension}"; url = dc.Url; attType = "document"; size = dc.SizeBytes;
                break;
        }

        const string sql = @"
            INSERT INTO ""message"".""history"" (
                room_id, sender_id, type, content,
                attachment_url, attachment_type, attachment_size, attachment_duration, attachment_thumb, attachment_waveform
            )
            VALUES (
                @roomId, @senderId, @type, @content,
                @attUrl, @attType, @attSize, @attDur, @attThumb, @attWave
            )
            RETURNING id, sent_at";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("roomId", message.RoomId);
        cmd.Parameters.AddWithValue("senderId", message.SenderId);
        cmd.Parameters.AddWithValue("type", (short)message.MessageType);
        cmd.Parameters.AddWithValue("content", (object?)contentOpt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("attUrl", (object?)url ?? DBNull.Value);
        cmd.Parameters.AddWithValue("attType", (object?)attType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("attSize", (object?)size ?? DBNull.Value);
        cmd.Parameters.AddWithValue("attDur", (object?)dur ?? DBNull.Value);
        cmd.Parameters.AddWithValue("attThumb", (object?)thumb ?? DBNull.Value);
        cmd.Parameters.AddWithValue("attWave", (object?)wave ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("INSERT no retornó datos de RETURNING");

        var genId = reader.GetInt64(0);
        var genDate = reader.GetDateTime(1);

        return message with { Id = genId, CreatedAt = DateTime.SpecifyKind(genDate, DateTimeKind.Utc) };
    }

    public async Task<List<Message>> FindByRoomIdAsync(long roomId, int limit, int offset)
    {
        const string sql = @"
            SELECT id, room_id, sender_id, type, content, sent_at,
                   attachment_url, attachment_size, attachment_duration, attachment_thumb, attachment_waveform, status
            FROM ""message"".""history""
            WHERE room_id = @roomId
            ORDER BY sent_at DESC
            LIMIT @limit
            OFFSET @offset";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("roomId", roomId);
        cmd.Parameters.AddWithValue("limit", limit);
        cmd.Parameters.AddWithValue("offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync();
        var messages = new List<Message>();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            var rId = reader.GetInt64(1);
            var sId = reader.GetInt64(2);
            var mType = (MessageType)reader.GetInt16(3);
            var contentStr = reader.IsDBNull(4) ? null : reader.GetString(4);
            var sentAt = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc);
            var attUrl = reader.IsDBNull(6) ? null : reader.GetString(6);
            var attSize = reader.IsDBNull(7) ? (long?)null : reader.GetInt64(7);
            var attDur = reader.IsDBNull(8) ? (double?)null : reader.GetDouble(8);
            var attThumb = reader.IsDBNull(9) ? null : reader.GetString(9);
            var attWave = reader.IsDBNull(10) ? null : reader.GetString(10);
            var statusVal = reader.IsDBNull(11) ? (short)1 : reader.GetInt16(11);

            // Reconstrucción del ADT desde columnas planas (idéntico al Scala)
            MessageContent content = mType switch
            {
                MessageType.Text => new TextContent(contentStr ?? ""),
                MessageType.Photo => new PhotoContent(attUrl ?? "", attSize, contentStr),
                MessageType.Video => new VideoContent(attUrl ?? "", attDur ?? 0.0, attSize, attThumb),
                MessageType.Audio => new AudioContent(attUrl ?? "", attDur ?? 0.0, attWave),
                MessageType.Sticker => ParseStickerContent(contentStr),
                MessageType.Document => ParseDocumentContent(contentStr, attUrl, attSize),
                _ => new TextContent(contentStr ?? "")
            };

            var isRead = statusVal == 2;
            messages.Add(new Message(id, rId, sId, mType, content, sentAt, isRead));
        }

        return messages;
    }

    private static DocumentContent ParseDocumentContent(string? contentStr, string? attUrl, long? attSize)
    {
        var fullName = contentStr ?? "unknown.bin";
        var dotIdx = fullName.LastIndexOf('.');
        var (name, ext) = dotIdx > 0
            ? (fullName[..dotIdx], fullName[(dotIdx + 1)..])
            : (fullName, "");
        return new DocumentContent(attUrl ?? "", name, ext, attSize ?? 0L);
    }

    private static StickerContent ParseStickerContent(string? contentStr)
    {
        if (string.IsNullOrEmpty(contentStr)) return new StickerContent("");
        var parts = contentStr.Split('|');
        var isAnimated = parts.Length > 1 && parts[1] == "1";
        return new StickerContent(parts[0], isAnimated);
    }
}
