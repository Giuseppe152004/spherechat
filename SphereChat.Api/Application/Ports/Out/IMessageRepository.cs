using SphereChat.Api.Domain.Models.Chat;

namespace SphereChat.Api.Application.Ports.Out;

/// <summary>
/// Contrato funcional de persistencia para mensajes.
/// Equivalente a MessageRepository.scala
/// </summary>
public interface IMessageRepository
{
    Task<Message> SaveAsync(Message message);
    Task<List<Message>> FindByRoomIdAsync(long roomId, int limit, int offset);
}
