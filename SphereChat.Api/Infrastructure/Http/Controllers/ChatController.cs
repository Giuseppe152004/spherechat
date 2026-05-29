using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SphereChat.Api.Application.DTOs;
using SphereChat.Api.Application.Ports.In;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Chat;

namespace SphereChat.Api.Infrastructure.Http.Controllers;

// ═══════════════════════════════════════════════════════════════════
// DTOs Estrictos — SIN senderId (La seguridad JWT lo inyecta)
// Equivalente exacto a los DTOs en ChatEndpoints.scala
// ═══════════════════════════════════════════════════════════════════

public record SendTextRequest(long RoomId, string Text);
public record SendImageRequest(long RoomId, string Url, int Width, int Height, string? Caption = null);
public record SendVideoRequest(long RoomId, string Url, double DurationSeconds, long? SizeBytes = null, string? Thumb = null);
public record SendAudioRequest(long RoomId, string Url, double DurationSeconds, string? Waveform = null);
public record SendStickerRequest(long RoomId, string StickerId, bool IsAnimated = false);
public record SendDocumentRequest(long RoomId, string Url, string FileName, string Extension, long SizeBytes);
public record SendMessageResponse(long MessageId, string Status);

/// <summary>
/// Controlador HTTP para el módulo de Chat — Tapir + JWT Middleware.
/// Equivalente a ChatEndpoints.scala
/// </summary>
[ApiController]
[Route("api/v1/chat")]
[Authorize]
[Tags("Chat - Mensajería")]
public class ChatController : ControllerBase
{
    private readonly ISendMessageUseCase _sendMessageUseCase;
    private readonly IMessageRepository _messageRepository;

    public ChatController(ISendMessageUseCase sendMessageUseCase, IMessageRepository messageRepository)
    {
        _sendMessageUseCase = sendMessageUseCase;
        _messageRepository = messageRepository;
    }

    private long GetUserId() =>
        long.Parse(User.FindFirst("userId")?.Value
            ?? throw new UnauthorizedAccessException("userId no encontrado en el token"));

    // ─── Endpoints de Escritura (POST) ───

    /// <summary>Envía un mensaje de texto a una sala</summary>
    [HttpPost("messages/text")]
    public Task<IActionResult> SendText([FromBody] SendTextRequest req) =>
        ExecuteCommand(new SendMessageCommand(req.RoomId, GetUserId(), (short)MessageType.Text, Text: req.Text));

    /// <summary>Notifica la subida de una imagen a una sala</summary>
    [HttpPost("messages/image")]
    public Task<IActionResult> SendImage([FromBody] SendImageRequest req) =>
        ExecuteCommand(new SendMessageCommand(req.RoomId, GetUserId(), (short)MessageType.Photo, Url: req.Url, Caption: req.Caption));

    /// <summary>Notifica la subida de un video a una sala</summary>
    [HttpPost("messages/video")]
    public Task<IActionResult> SendVideo([FromBody] SendVideoRequest req) =>
        ExecuteCommand(new SendMessageCommand(req.RoomId, GetUserId(), (short)MessageType.Video,
            Url: req.Url, DurationSeconds: req.DurationSeconds, SizeBytes: req.SizeBytes, Thumb: req.Thumb));

    /// <summary>Notifica la subida de una nota de audio a una sala</summary>
    [HttpPost("messages/audio")]
    public Task<IActionResult> SendAudio([FromBody] SendAudioRequest req) =>
        ExecuteCommand(new SendMessageCommand(req.RoomId, GetUserId(), (short)MessageType.Audio,
            Url: req.Url, DurationSeconds: req.DurationSeconds, Waveform: req.Waveform));

    /// <summary>Envía un sticker a una sala</summary>
    [HttpPost("messages/sticker")]
    public Task<IActionResult> SendSticker([FromBody] SendStickerRequest req) =>
        ExecuteCommand(new SendMessageCommand(req.RoomId, GetUserId(), (short)MessageType.Sticker, StickerId: req.StickerId, IsAnimated: req.IsAnimated));

    /// <summary>Notifica la subida de un documento a una sala</summary>
    [HttpPost("messages/document")]
    public Task<IActionResult> SendDocument([FromBody] SendDocumentRequest req) =>
        ExecuteCommand(new SendMessageCommand(req.RoomId, GetUserId(), (short)MessageType.Document,
            Url: req.Url, SizeBytes: req.SizeBytes, FileName: req.FileName, FileExtension: req.Extension));

    // ─── Endpoint de Lectura (GET) — Historial ───

    /// <summary>Consulta el historial de mensajes de una sala con paginación</summary>
    [HttpGet("rooms/{roomId:long}/messages")]
    public async Task<IActionResult> GetHistory(
        long roomId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var messages = await _messageRepository.FindByRoomIdAsync(roomId, limit, offset);
        var dtos = messages.Select(MessageDto.FromDomain).ToList();
        return Ok(dtos);
    }

    // ─── Lógica de Ejecución Reutilizable ───

    private async Task<IActionResult> ExecuteCommand(SendMessageCommand command)
    {
        try
        {
            var msgId = await _sendMessageUseCase.ExecuteAsync(command);
            return Ok(new SendMessageResponse(msgId, "SENT_SUCCESSFULLY"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest($"Petición Inválida: {ex.Message}");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error Interno: {ex.Message}");
        }
    }
}
