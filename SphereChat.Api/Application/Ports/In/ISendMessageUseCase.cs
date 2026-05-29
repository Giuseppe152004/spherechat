namespace SphereChat.Api.Application.Ports.In;

/// <summary>
/// DTO de Comando para el Caso de Uso (independiente de HTTP).
/// Equivalente a SendMessageCommand de Scala.
/// </summary>
public record SendMessageCommand(
    long RoomId,
    long SenderId,
    short MessageType,
    string? Text = null,
    string? Url = null,
    double? DurationSeconds = null,
    long? SizeBytes = null,
    string? Caption = null,
    string? Thumb = null,
    string? Waveform = null,
    string? StickerId = null,
    bool IsAnimated = false,
    string? FileName = null,
    string? FileExtension = null
);

/// <summary>
/// Caso de uso para enviar mensajes.
/// Equivalente a SendMessageUseCase.scala
/// </summary>
public interface ISendMessageUseCase
{
    Task<long> ExecuteAsync(SendMessageCommand command);
}
