namespace SphereChat.Api.Domain.Models.Chat;

/// <summary>
/// Entidad Raíz del módulo de Chat.
/// Equivalente al case class Message de Scala.
/// </summary>
public record Message(
    long Id,
    long RoomId,
    long SenderId,
    MessageType MessageType,
    MessageContent Content,
    DateTime CreatedAt,
    bool IsRead
);
