namespace SphereChat.Api.Application.Ports.Out;

/// <summary>
/// Mantiene un registro de las conexiones activas en memoria para enrutar llamadas.
/// </summary>
public interface ICallSessionTracker
{
    bool UserConnected(long userId, string connectionId);
    long? UserDisconnected(string connectionId);
    string? GetConnectionIdForUser(long userId);
}
