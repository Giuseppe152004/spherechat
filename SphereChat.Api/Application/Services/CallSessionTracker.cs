using System.Collections.Concurrent;
using System.Linq;
using SphereChat.Api.Application.Ports.Out;

namespace SphereChat.Api.Application.Services;

public class CallSessionTracker : ICallSessionTracker
{
    // Mapea UserId -> Set de ConnectionIds
    private readonly ConcurrentDictionary<long, HashSet<string>> _userConnections = new();
    
    // Mapea ConnectionId -> UserId (para limpiezas rápidas en desconexión)
    private readonly ConcurrentDictionary<string, long> _connectionUsers = new();

    public bool UserConnected(long userId, string connectionId)
    {
        _connectionUsers[connectionId] = userId;
        var connections = _userConnections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (connections)
        {
            connections.Add(connectionId);
            return connections.Count == 1; // True si es su primera y única conexión (recién conectado)
        }
    }

    public long? UserDisconnected(string connectionId)
    {
        if (_connectionUsers.TryRemove(connectionId, out var userId))
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    connections.Remove(connectionId);
                    if (connections.Count == 0)
                    {
                        _userConnections.TryRemove(userId, out _);
                        return userId; // Totalmente desconectado
                    }
                }
            }
        }
        return null; // Aún tiene otras conexiones activas
    }

    public string? GetConnectionIdForUser(long userId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                return connections.FirstOrDefault();
            }
        }
        return null;
    }
}
