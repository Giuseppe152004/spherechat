using SphereChat.Api.Application.Ports.In;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Chat;

namespace SphereChat.Api.Application.Services;

/// <summary>
/// Servicio que orquesta el envío de mensajes.
/// Equivalente a SendMessageService.scala
/// </summary>
public class SendMessageService : ISendMessageUseCase
{
    private readonly IMessageRepository _messageRepository;

    public SendMessageService(IMessageRepository messageRepository)
    {
        _messageRepository = messageRepository;
    }

    public async Task<long> ExecuteAsync(SendMessageCommand command)
    {
        var content = BuildContent(command);

        // ID es 0 temporalmente, la BD lo asignará vía RETURNING
        var message = new Message(
            Id: 0L,
            RoomId: command.RoomId,
            SenderId: command.SenderId,
            MessageType: (MessageType)command.MessageType,
            Content: content,
            CreatedAt: DateTime.UtcNow,
            IsRead: false
        );

        var savedMessage = await _messageRepository.SaveAsync(message);
        return savedMessage.Id;
    }

    private static MessageContent BuildContent(SendMessageCommand cmd)
    {
        var msgType = (MessageType)cmd.MessageType;
        return msgType switch
        {
            MessageType.Text => new TextContent(
                cmd.Text ?? throw new ArgumentException("Text is required for TEXT")),

            MessageType.Audio => new AudioContent(
                cmd.Url ?? throw new ArgumentException("URL is required"),
                cmd.DurationSeconds ?? throw new ArgumentException("Duration is required"),
                cmd.Waveform),

            MessageType.Photo => new PhotoContent(
                cmd.Url ?? throw new ArgumentException("URL is required"),
                cmd.SizeBytes,
                cmd.Caption),

            MessageType.Video => new VideoContent(
                cmd.Url ?? throw new ArgumentException("URL is required"),
                cmd.DurationSeconds ?? 0.0,
                cmd.SizeBytes,
                cmd.Thumb),

            MessageType.Sticker => new StickerContent(
                StickerId: cmd.StickerId ?? throw new ArgumentException("StickerId requerido"),
                IsAnimated: cmd.IsAnimated
            ),

            MessageType.Document => new DocumentContent(
                cmd.Url ?? throw new ArgumentException("URL is required for DOCUMENT"),
                cmd.FileName ?? throw new ArgumentException("fileName is required for DOCUMENT"),
                cmd.FileExtension ?? throw new ArgumentException("fileExtension is required for DOCUMENT"),
                cmd.SizeBytes ?? throw new ArgumentException("sizeBytes is required for DOCUMENT")),

            _ => throw new ArgumentException($"MessageType desconocido: {cmd.MessageType}")
        };
    }
}
