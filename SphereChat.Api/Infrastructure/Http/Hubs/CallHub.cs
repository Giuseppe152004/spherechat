using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SphereChat.Api.Application.Ports.In;
using SphereChat.Api.Application.Ports.Out;
using System.Security.Claims;

namespace SphereChat.Api.Infrastructure.Http.Hubs;

/// <summary>
/// Hub de señalización WebRTC para llamadas de voz/video.
/// IMPORTANTE: Los SDPs e ICE Candidates viajan como strings JSON crudos
/// para evitar que SignalR altere propiedades durante la serialización.
/// (Arquitectura probada y funcional en el proyecto Ajedrez)
/// </summary>
[Authorize]
public class CallHub : Hub
{
    private readonly ICallSessionTracker _tracker;
    private readonly ISendMessageUseCase _sendMessage;
    private readonly ILogger<CallHub> _logger;

    public CallHub(ICallSessionTracker tracker, ISendMessageUseCase sendMessage, ILogger<CallHub> logger)
    {
        _tracker = tracker;
        _sendMessage = sendMessage;
        _logger = logger;
    }

    private long GetUserId()
    {
        var idStr = Context.User?.FindFirst("userId")?.Value 
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
        if (long.TryParse(idStr, out var id))
            return id;
            
        _logger.LogWarning("⚠️ No se pudo extraer el userId del token JWT. Claims disponibles: {Claims}", 
            string.Join(", ", Context.User?.Claims.Select(c => c.Type) ?? Array.Empty<string>()));
        return 0;
    }

    // ════════════════════════════════════════════════════════════
    // Conexión / Desconexión
    // ════════════════════════════════════════════════════════════

    public override Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId > 0)
        {
            _tracker.UserConnected(userId, Context.ConnectionId);
            _logger.LogInformation("✅ SIGNALR CONNECTED: Usuario {UserId} conectado al hub de llamadas. ConnectionId: {ConnId}", userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("❌ SIGNALR CONNECTED BUT UNAUTHENTICATED: Alguien se conectó pero no tiene userId. ConnectionId: {ConnId}", Context.ConnectionId);
        }
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _tracker.UserDisconnected(Context.ConnectionId);
        _logger.LogInformation("Conexión {ConnId} terminada.", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    // ════════════════════════════════════════════════════════════
    // Señalización WebRTC (Strings crudos - NO deserializar)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia una llamada. El sdpOffer es un JSON string crudo que se reenvía tal cual.
    /// </summary>
    public async Task InitiateCall(long roomId, long receiverId, string sdpOffer)
    {
        var callerId = GetUserId();
        var receiverConnection = _tracker.GetConnectionIdForUser(receiverId);

        if (receiverConnection != null)
        {
            _logger.LogInformation("📞 Usuario {Caller} iniciando llamada a {Receiver} en Room {Room}", callerId, receiverId, roomId);
            await Clients.Client(receiverConnection).SendAsync("IncomingCall", callerId, sdpOffer);
        }
        else
        {
            _logger.LogInformation("📞 Usuario {Receiver} offline. Guardando llamada perdida de {Caller}", receiverId, callerId);
            await SaveCallRecordAsync(roomId, callerId, "📞 Llamada perdida");
            await Clients.Caller.SendAsync("CallFailed", receiverId, "Usuario no disponible o desconectado.");
        }
    }

    /// <summary>
    /// Responde a una llamada entrante. El sdpAnswer es un JSON string crudo.
    /// </summary>
    public async Task AcceptCall(long callerId, string sdpAnswer)
    {
        var receiverId = GetUserId();
        var callerConnection = _tracker.GetConnectionIdForUser(callerId);

        if (callerConnection != null)
        {
            _logger.LogInformation("✅ Usuario {Receiver} aceptó la llamada de {Caller}", receiverId, callerId);
            await Clients.Client(callerConnection).SendAsync("CallAccepted", receiverId, sdpAnswer);
        }
    }

    /// <summary>
    /// Rechaza la llamada entrante.
    /// </summary>
    public async Task RejectCall(long roomId, long callerId)
    {
        var receiverId = GetUserId();
        var callerConnection = _tracker.GetConnectionIdForUser(callerId);

        _logger.LogInformation("❌ Usuario {Receiver} rechazó la llamada de {Caller}", receiverId, callerId);
        await SaveCallRecordAsync(roomId, receiverId, "📞 Llamada rechazada");

        if (callerConnection != null)
        {
            await Clients.Client(callerConnection).SendAsync("CallRejected", receiverId);
        }
    }

    /// <summary>
    /// Termina una llamada en curso. El frontend envía la duración en segundos.
    /// </summary>
    public async Task EndCall(long roomId, long targetId, int durationSeconds)
    {
        var myId = GetUserId();
        var targetConnection = _tracker.GetConnectionIdForUser(targetId);

        var durationStr = TimeSpan.FromSeconds(durationSeconds).ToString(@"mm\:ss");
        _logger.LogInformation("⏱️ [Métricas] Llamada entre {Caller} y {Receiver} finalizada. Duración total: {Duracion}", myId, targetId, durationStr);
        
        await SaveCallRecordAsync(roomId, myId, $"📞 Llamada finalizada - {durationStr}");

        if (targetConnection != null)
        {
            await Clients.Client(targetConnection).SendAsync("CallEnded", myId);
        }
    }

    /// <summary>
    /// Envía un ICE Candidate como JSON string crudo. Se reenvía tal cual sin deserializar.
    /// </summary>
    public async Task SendIceCandidate(long targetId, string candidateJson)
    {
        var myId = GetUserId();
        var targetConnection = _tracker.GetConnectionIdForUser(targetId);

        if (targetConnection != null)
        {
            _logger.LogDebug("🧊 ICE Candidate de {Sender} para {Target}", myId, targetId);
            await Clients.Client(targetConnection).SendAsync("ReceiveIceCandidate", myId, candidateJson);
        }
    }

    // ════════════════════════════════════════════════════════════
    // Persistencia en Base de Datos
    // ════════════════════════════════════════════════════════════

    private async Task SaveCallRecordAsync(long roomId, long senderId, string text)
    {
        try
        {
            var command = new SendMessageCommand(roomId, senderId, 1, Text: text);
            await _sendMessage.ExecuteAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo guardar el registro de la llamada en la base de datos.");
        }
    }
}
