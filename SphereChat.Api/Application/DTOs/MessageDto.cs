using SphereChat.Api.Domain.Models.Chat;

namespace SphereChat.Api.Application.DTOs;

/// <summary>
/// DTO de Lectura para el Historial de mensajes.
/// Equivalente a MessageDto de Scala (ChatEndpoints.scala).
/// </summary>
public record MessageDto(
    long Id,
    long RoomId,
    long SenderId,
    string MessageType,
    string? Text,
    string? Url,
    double? DurationSeconds,
    long? SizeBytes,
    string? Caption,
    string? Thumb,
    string? Waveform,
    string? StickerId,
    string? FileName,
    string? FileExtension,
    string CreatedAt,
    bool IsRead
)
{
    /// <summary>
    /// Convierte una entidad Message de dominio al DTO plano para el Frontend.
    /// Equivalente a MessageDto.fromDomain en Scala.
    /// </summary>
    public static MessageDto FromDomain(Message msg)
    {
        string? text = null, url = null, caption = null, thumb = null, waveform = null, stickerId = null, fileName = null, fileExt = null;
        double? dur = null;
        long? size = null;

        switch (msg.Content)
        {
            case TextContent tc:
                text = tc.Text;
                break;
            case PhotoContent pc:
                url = pc.Url; size = pc.SizeBytes; caption = pc.Caption;
                break;
            case VideoContent vc:
                url = vc.Url; dur = vc.DurationSeconds; size = vc.SizeBytes; thumb = vc.Thumb;
                break;
            case AudioContent ac:
                url = ac.Url; dur = ac.DurationSeconds; waveform = ac.Waveform;
                break;
            case StickerContent sc:
                stickerId = sc.StickerId;
                break;
            case DocumentContent dc:
                url = dc.Url; size = dc.SizeBytes; fileName = dc.FileName; fileExt = dc.FileExtension;
                break;
        }

        return new MessageDto(
            msg.Id, msg.RoomId, msg.SenderId,
            msg.MessageType.ToString().ToUpperInvariant(),
            text, url, dur, size, caption, thumb, waveform, stickerId, fileName, fileExt,
            msg.CreatedAt.ToString("o"),
            msg.IsRead
        );
    }
}
