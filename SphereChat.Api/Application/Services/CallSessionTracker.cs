using System.Collections.Concurrent;
using SphereChat.Api.Application.Ports.Out;

namespace SphereChat.Api.Application.Services;

public class CallSessionTracker : ICallSessionTracker
{
    // Mapea UserId -> ConnectionId
    private readonly ConcurrentDictionary<long, string> _userConnections = new();
    
    // Mapea ConnectionId -> UserId (para limpiezas rápidas en desconexión)
    private readonly ConcurrentDictionary<string, long> _connectionUsers = new();

    public void UserConnected(long userId, string connectionId)
    {
        _userConnections[userId] = connectionId;
        _connectionUsers[connectionId] = userId;
    }

    public void UserDisconnected(string connectionId)
    {
        if (_connectionUsers.TryRemove(connectionId, out var userId))
        {
            _userConnections.TryRemove(userId, out _);
        }
    }

    public string? GetConnectionIdForUser(long userId)
    {
        _userConnections.TryGetValue(userId, out var connectionId);
        return connectionId;
    }
}
