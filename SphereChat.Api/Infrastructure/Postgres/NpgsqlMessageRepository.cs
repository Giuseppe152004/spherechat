using Npgsql;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Chat;
using StackExchange.Redis;
using System.Text.Json;

namespace SphereChat.Api.Infrastructure.Postgres;

/// <summary>
/// Adaptador de Persistencia hacia "message"."history" usando Npgsql y Redis.
/// </summary>
public class NpgsqlMessageRepository : IMessageRepository
{
    private readonly string _connectionString;
    private readonly IConnectionMultiplexer _redis;

    public NpgsqlMessageRepository(string connectionString, IConnectionMultiplexer redis)
    {
        _connectionString = connectionString;
        _redis = redis;
    }

    private record RedisMessageDto(
        long Id, long RoomId, long SenderId, short Type, string? Content, 
        DateTime SentAt, string? AttUrl, long? AttSize, double? AttDur, 
        string? AttThumb, string? AttWave, short Status);

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
        var utcDate = DateTime.SpecifyKind(genDate, DateTimeKind.Utc);

        var finalMessage = message with { Id = genId, CreatedAt = DateTime.SpecifyKind(genDate, DateTimeKind.Utc) };

        // 🟢 Redis Pagination Layer: Add to ZSET for fast retrieval
        try
        {
            var db = _redis.GetDatabase();
            var cacheKey = $"chat:messages:{message.RoomId}";
            var dto = new RedisMessageDto(genId, message.RoomId, message.SenderId, (short)message.MessageType, contentOpt, utcDate, url, size, dur, thumb, wave, 1);
            var json = JsonSerializer.Serialize(dto);
            
            // Usar el SentAt.Ticks negativo para ordenar descendente, o simplemente ZSET rank con Id 
            await db.SortedSetAddAsync(cacheKey, json, genId); // Ordenar por ID ascendente
            await db.KeyExpireAsync(cacheKey, TimeSpan.FromDays(7));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[REDIS] Error saving message to cache: {ex.Message}");
        }

        return finalMessage;
    }

    public async Task<List<Message>> FindByRoomIdAsync(long roomId, int limit, int offset)
    {
        var db = _redis.GetDatabase();
        var cacheKey = $"chat:messages:{roomId}";
        
        try
        {
            // 🟢 Redis Fast Path: Try fetching paginated history from Redis
            var cachedCount = await db.SortedSetLengthAsync(cacheKey);
            if (cachedCount > 0 && offset + limit <= cachedCount)
            {
                // Extraer ordenando por puntaje DESCENDENTE (más nuevos primero)
                var cached = await db.SortedSetRangeByRankAsync(cacheKey, 0, -1, Order.Descending);
                var paginated = cached.Skip(offset).Take(limit).ToList();
                
                if (paginated.Count == limit || (offset + paginated.Count >= cachedCount))
                {
                    var result = new List<Message>();
                    foreach (var json in paginated)
                    {
                        var dto = JsonSerializer.Deserialize<RedisMessageDto>((string)json!);
                        if (dto != null) result.Add(MapDtoToMessage(dto));
                    }
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[REDIS] Error reading cache: {ex.Message}");
        }

        // 🟡 Fallback Path: Postgres
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
            var mType = reader.GetInt16(3);
            var contentStr = reader.IsDBNull(4) ? null : reader.GetString(4);
            var sentAt = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc);
            var attUrl = reader.IsDBNull(6) ? null : reader.GetString(6);
            var attSize = reader.IsDBNull(7) ? (long?)null : reader.GetInt64(7);
            var attDur = reader.IsDBNull(8) ? (double?)null : reader.GetDouble(8);
            var attThumb = reader.IsDBNull(9) ? null : reader.GetString(9);
            var attWave = reader.IsDBNull(10) ? null : reader.GetString(10);
            var statusVal = reader.IsDBNull(11) ? (short)1 : reader.GetInt16(11);

            var dto = new RedisMessageDto(id, rId, sId, mType, contentStr, sentAt, attUrl, attSize, attDur, attThumb, attWave, statusVal);
            messages.Add(MapDtoToMessage(dto));
        }

        // 🔄 Repopulate Redis Cache si estamos cargando la primera página
        if (offset == 0 && messages.Count > 0)
        {
            try
            {
                await db.KeyDeleteAsync(cacheKey);
                var batch = db.CreateBatch();
                foreach (var msg in messages)
                {
                    string? contentOpt = msg.Content switch { TextContent tc => tc.Text, PhotoContent pc => pc.Caption, StickerContent sc => $"{sc.StickerId}|{(sc.IsAnimated ? "1" : "0")}", DocumentContent dc => $"{dc.FileName}.{dc.FileExtension}", _ => null };
                    string? attUrl = msg.Content switch { PhotoContent pc => pc.Url, VideoContent vc => vc.Url, AudioContent ac => ac.Url, DocumentContent dc => dc.Url, _ => null };
                    long? size = msg.Content switch { PhotoContent pc => pc.SizeBytes, VideoContent vc => vc.SizeBytes, DocumentContent dc => dc.SizeBytes, _ => (long?)null };
                    double? dur = msg.Content switch { VideoContent vc => vc.DurationSeconds, AudioContent ac => ac.DurationSeconds, _ => (double?)null };
                    string? thumb = msg.Content switch { VideoContent vc => vc.Thumb, _ => null };
                    string? wave = msg.Content switch { AudioContent ac => ac.Waveform, _ => null };
                    var statusVal = (short)(msg.IsRead ? 2 : 1);
                    
                    var dto = new RedisMessageDto(msg.Id, msg.RoomId, msg.SenderId, (short)msg.MessageType, contentOpt, msg.CreatedAt, attUrl, size, dur, thumb, wave, statusVal);
                    _ = batch.SortedSetAddAsync(cacheKey, JsonSerializer.Serialize(dto), msg.Id);
                }
                _ = batch.KeyExpireAsync(cacheKey, TimeSpan.FromDays(7));
                batch.Execute();
            }
            catch { /* ignora errores asíncronos de repoblación */ }
        }

        return messages;
    }

    private Message MapDtoToMessage(RedisMessageDto dto)
    {
        var mType = (MessageType)dto.Type;
        MessageContent content = mType switch
        {
            MessageType.Text => new TextContent(dto.Content ?? ""),
            MessageType.Photo => new PhotoContent(dto.AttUrl ?? "", dto.AttSize, dto.Content),
            MessageType.Video => new VideoContent(dto.AttUrl ?? "", dto.AttDur ?? 0.0, dto.AttSize, dto.AttThumb),
            MessageType.Audio => new AudioContent(dto.AttUrl ?? "", dto.AttDur ?? 0.0, dto.AttWave),
            MessageType.Sticker => ParseStickerContent(dto.Content),
            MessageType.Document => ParseDocumentContent(dto.Content, dto.AttUrl, dto.AttSize),
            _ => new TextContent(dto.Content ?? "")
        };

        var isRead = dto.Status == 2;
        return new Message(dto.Id, dto.RoomId, dto.SenderId, mType, content, dto.SentAt, isRead);
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
