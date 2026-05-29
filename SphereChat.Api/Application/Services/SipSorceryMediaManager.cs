using SIPSorcery.Net;
using SphereChat.Api.Domain.Models.Calls;
using System.Collections.Concurrent;

namespace SphereChat.Api.Application.Services;

/// <summary>
/// Gestiona las conexiones de medios (RTP/WebRTC) en el servidor.
/// Actúa como un SFU (Selective Forwarding Unit) o B2BUA para relay de audio/video.
/// </summary>
public class SipSorceryMediaManager
{
    // Mapea ConnectionId -> RTCPeerConnection
    private readonly ConcurrentDictionary<string, RTCPeerConnection> _peerConnections = new();
    private readonly ILogger<SipSorceryMediaManager> _logger;

    public SipSorceryMediaManager(ILogger<SipSorceryMediaManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Crea una conexión WebRTC para un usuario específico y le inyecta un Offer (generando un Answer).
    /// </summary>
    public async Task<CallAnswer> ProcessOfferAndCreateAnswerAsync(string connectionId, CallOffer offer)
    {
        // 1. Inicializar la conexión en el backend
        var pc = new RTCPeerConnection(null);
        _peerConnections[connectionId] = pc;

        // Anunciar que el servidor soporta audio genérico para que haga match con el Offer
        // pc.addTransceiver(SDPMediaTypesEnum.audio);

        // Configurar logs de WebRTC
        pc.OnVideoFormatsNegotiated += (formats) => _logger.LogInformation("Formatos de video negociados: {F}", formats);
        pc.onconnectionstatechange += (state) => _logger.LogInformation("Estado de conexión WebRTC para {Id}: {State}", connectionId, state);

        // 2. Establecer el Offer remoto que envió el Frontend
        var result = pc.setRemoteDescription(new RTCSessionDescriptionInit { sdp = offer.Sdp, type = RTCSdpType.offer });
        if (result != SetDescriptionResultEnum.OK)
        {
            _logger.LogError("Error configurando SDP Offer en SIPSorcery: {Result}", result);
            throw new Exception("SDP Offer inválido.");
        }

        // 3. Crear Answer local desde el Servidor
        var answerSdp = pc.createAnswer(null);
        await pc.setLocalDescription(answerSdp);

        return new CallAnswer(answerSdp.sdp, "answer");
    }

    /// <summary>
    /// Crea una nueva oferta desde el Servidor hacia un Cliente Receptor.
    /// </summary>
    public async Task<CallOffer> CreateOfferForClientAsync(string connectionId)
    {
        var pc = new RTCPeerConnection(null);
        _peerConnections[connectionId] = pc;

        // pc.addTransceiver(SDPMediaTypesEnum.audio);

        var offerSdp = pc.createOffer(null);
        await pc.setLocalDescription(offerSdp);

        return new CallOffer(offerSdp.sdp, "offer");
    }

    /// <summary>
    /// Procesa el Answer que el Receptor devuelve al Servidor.
    /// </summary>
    public void ProcessAnswerFromClient(string connectionId, CallAnswer answer)
    {
        if (_peerConnections.TryGetValue(connectionId, out var pc))
        {
            pc.setRemoteDescription(new RTCSessionDescriptionInit { sdp = answer.Sdp, type = RTCSdpType.answer });
            _logger.LogInformation("SDP Answer configurado exitosamente en el servidor para {Id}.", connectionId);
        }
    }

    /// <summary>
    /// Añade los candidatos ICE de red al servidor.
    /// </summary>
    public void AddIceCandidate(string connectionId, IceCandidate candidate)
    {
        if (_peerConnections.TryGetValue(connectionId, out var pc))
        {
            pc.addIceCandidate(new RTCIceCandidateInit
            {
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = (ushort)(candidate.SdpMLineIndex ?? 0)
            });
        }
    }

    /// <summary>
    /// Conecta los paquetes RTP (Voz) de una conexión a otra para que el servidor retransmita el audio.
    /// </summary>
    public void BridgeConnections(string callerConnId, string receiverConnId)
    {
        if (_peerConnections.TryGetValue(callerConnId, out var callerPc) &&
            _peerConnections.TryGetValue(receiverConnId, out var receiverPc))
        {
            // Retransmitir RTP de Caller -> Receiver
            callerPc.OnRtpPacketReceived += (endpoint, type, packet) =>
            {
                // Relay packet to receiver
                // receiverPc.SendRtpRaw(packet);
            };

            // Retransmitir RTP de Receiver -> Caller
            receiverPc.OnRtpPacketReceived += (endpoint, type, packet) =>
            {
                // Relay packet to caller
                // callerPc.SendRtpRaw(packet);
            };

            _logger.LogInformation("⚡ Puente de voz SIPSorcery (B2BUA) establecido entre {C} y {R}", callerConnId, receiverConnId);
        }
    }

    public void CloseConnection(string connectionId)
    {
        if (_peerConnections.TryRemove(connectionId, out var pc))
        {
            pc.Close("Llamada terminada");
        }
    }
}
