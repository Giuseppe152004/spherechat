namespace SphereChat.Api.Application.Ports.Out;

/// <summary>
/// Mantiene un registro de las conexiones activas en memoria para enrutar llamadas.
/// </summary>
public interface ICallSessionTracker
{
    void UserConnected(long userId, string connectionId);
    void UserDisconnected(string connectionId);
    string? GetConnectionIdForUser(long userId);
}
