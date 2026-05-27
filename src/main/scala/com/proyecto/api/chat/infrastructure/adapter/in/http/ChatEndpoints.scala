package com.proyecto.api.chat.infrastructure.adapter.in.http

import cats.effect.IO
import sttp.tapir.*
import sttp.tapir.generic.auto.*
import sttp.tapir.json.circe.*
import sttp.tapir.server.ServerEndpoint
import io.circe.generic.auto.*
import com.proyecto.api.chat.application.port.in.{SendMessageUseCase, SendMessageCommand}
import com.proyecto.api.chat.application.port.out.MessageRepository
import com.proyecto.api.chat.domain.model.{MessageType, MessageContent, RoomId}
import com.proyecto.api.auth.infrastructure.security.JwtService

// ═══════════════════════════════════════════════════════════════════
// DTOs Estrictos — SIN senderId (La seguridad JWT lo inyecta)
// ═══════════════════════════════════════════════════════════════════

case class SendTextRequest(roomId: Long, text: String) {
  def toCommand(senderId: Long): SendMessageCommand = SendMessageCommand(
    roomId, senderId, MessageType.TEXT.code, Some(text), None, None, None, None, None, None, None
  )
}

case class SendImageRequest(roomId: Long, url: String, width: Int, height: Int, caption: Option[String] = None) {
  def toCommand(senderId: Long): SendMessageCommand = SendMessageCommand(
    roomId, senderId, MessageType.PHOTO.code, None, Some(url), None, None, caption, None, None, None
  )
}

case class SendVideoRequest(roomId: Long, url: String, durationSeconds: Double, sizeBytes: Option[Long] = None, thumb: Option[String] = None) {
  def toCommand(senderId: Long): SendMessageCommand = SendMessageCommand(
    roomId, senderId, MessageType.VIDEO.code, None, Some(url), Some(durationSeconds), sizeBytes, None, thumb, None, None
  )
}

case class SendAudioRequest(roomId: Long, url: String, durationSeconds: Double, waveform: Option[String] = None) {
  def toCommand(senderId: Long): SendMessageCommand = SendMessageCommand(
    roomId, senderId, MessageType.AUDIO.code, None, Some(url), Some(durationSeconds), None, None, None, waveform, None
  )
}

case class SendStickerRequest(roomId: Long, stickerId: String) {
  def toCommand(senderId: Long): SendMessageCommand = SendMessageCommand(
    roomId, senderId, MessageType.STICKER.code, None, None, None, None, None, None, None, Some(stickerId)
  )
}

case class SendDocumentRequest(roomId: Long, url: String, fileName: String, extension: String, sizeBytes: Long) {
  def toCommand(senderId: Long): SendMessageCommand = SendMessageCommand(
    roomId, senderId, MessageType.DOCUMENT.code, None, Some(url), None, Some(sizeBytes), None, None, None, None,
    Some(fileName), Some(extension)
  )
}

case class SendMessageResponse(messageId: Long, status: String)

// ── DTO de Lectura para el Historial ──
case class MessageDto(
  id: Long,
  roomId: Long,
  senderId: Long,
  messageType: String,
  text: Option[String],
  url: Option[String],
  durationSeconds: Option[Double],
  sizeBytes: Option[Long],
  caption: Option[String],
  thumb: Option[String],
  waveform: Option[String],
  stickerId: Option[String],
  fileName: Option[String],
  fileExtension: Option[String],
  createdAt: String,
  isRead: Boolean
)

object MessageDto {
  def fromDomain(msg: com.proyecto.api.chat.domain.model.Message): MessageDto = {
    val (text, url, dur, size, caption, thumb, wave, sticker, fName, fExt) = msg.content match {
      case MessageContent.TextContent(t) =>
        (Some(t), None, None, None, None, None, None, None, None, None)
      case MessageContent.PhotoContent(u, s, c) =>
        (None, Some(u), None, s, c, None, None, None, None, None)
      case MessageContent.VideoContent(u, d, s, th) =>
        (None, Some(u), Some(d), s, None, th, None, None, None, None)
      case MessageContent.AudioContent(u, d, w) =>
        (None, Some(u), Some(d), None, None, None, w, None, None, None)
      case MessageContent.StickerContent(st) =>
        (None, None, None, None, None, None, None, Some(st), None, None)
      case MessageContent.DocumentContent(u, fn, ext, sz) =>
        (None, Some(u), None, Some(sz), None, None, None, None, Some(fn), Some(ext))
    }
    MessageDto(
      msg.id.value, msg.roomId.value, msg.senderId.value,
      msg.messageType.toString, text, url, dur, size, caption, thumb, wave, sticker, fName, fExt,
      msg.createdAt.toString, msg.isRead
    )
  }
}

// ═══════════════════════════════════════════════════════════════════
// Controlador HTTP — Tapir + JWT Middleware
// ═══════════════════════════════════════════════════════════════════

class ChatEndpoints(sendMessageUseCase: SendMessageUseCase, messageRepository: MessageRepository, jwtService: JwtService) {

  // Base Segura POST: Intercepta el Bearer Token, lo valida y retorna el userId (Long)
  private val securePostBase = endpoint.post
    .securityIn(auth.bearer[String]())
    .errorOut(stringBody)
    .serverSecurityLogic(token => jwtService.validateToken(token))

  // Base Segura GET
  private val secureGetBase = endpoint.get
    .securityIn(auth.bearer[String]())
    .errorOut(stringBody)
    .serverSecurityLogic(token => jwtService.validateToken(token))

  // ─── Endpoints de Escritura (POST) ───

  val sendTextEndpoint = securePostBase
    .in("api" / "v1" / "chat" / "messages" / "text")
    .in(jsonBody[SendTextRequest].description("Mensaje de texto plano o emojis Unicode"))
    .out(jsonBody[SendMessageResponse])
    .description("Envía un mensaje de texto a una sala")

  val sendImageEndpoint = securePostBase
    .in("api" / "v1" / "chat" / "messages" / "image")
    .in(jsonBody[SendImageRequest].description("Metadatos de imagen ya subida al Storage"))
    .out(jsonBody[SendMessageResponse])
    .description("Notifica la subida de una imagen a una sala")

  val sendVideoEndpoint = securePostBase
    .in("api" / "v1" / "chat" / "messages" / "video")
    .in(jsonBody[SendVideoRequest].description("Metadatos de video ya subido al Storage"))
    .out(jsonBody[SendMessageResponse])
    .description("Notifica la subida de un video a una sala")

  val sendAudioEndpoint = securePostBase
    .in("api" / "v1" / "chat" / "messages" / "audio")
    .in(jsonBody[SendAudioRequest].description("Metadatos de nota de voz ya subida al Storage"))
    .out(jsonBody[SendMessageResponse])
    .description("Notifica la subida de una nota de audio a una sala")

  val sendStickerEndpoint = securePostBase
    .in("api" / "v1" / "chat" / "messages" / "sticker")
    .in(jsonBody[SendStickerRequest].description("ID de un sticker precargado del sistema"))
    .out(jsonBody[SendMessageResponse])
    .description("Envía un sticker a una sala")

  val sendDocumentEndpoint = securePostBase
    .in("api" / "v1" / "chat" / "messages" / "document")
    .in(jsonBody[SendDocumentRequest].description("Metadatos de documento (PDF, Word, Excel) ya subido al Storage"))
    .out(jsonBody[SendMessageResponse])
    .description("Notifica la subida de un documento a una sala")

  // ─── Endpoint de Lectura (GET) — Historial ───

  val getHistoryEndpoint = secureGetBase
    .in("api" / "v1" / "chat" / "rooms" / path[Long]("roomId") / "messages")
    .in(query[Option[Int]]("limit").description("Máximo de mensajes a retornar (default 50)"))
    .in(query[Option[Int]]("offset").description("Desplazamiento para paginación (default 0)"))
    .out(jsonBody[List[MessageDto]])
    .description("Consulta el historial de mensajes de una sala con paginación")

  // ─── Lógica de Ejecución Reutilizable ───

  private def executeLogic(command: SendMessageCommand): IO[Either[String, SendMessageResponse]] = {
    sendMessageUseCase.execute(command).map { msgId =>
      Right[String, SendMessageResponse](SendMessageResponse(msgId, "SENT_SUCCESSFULLY"))
    }.handleErrorWith {
      case e: IllegalArgumentException => IO.pure(Left(s"Petición Inválida: ${e.getMessage}"))
      case e: Exception => IO.pure(Left(s"Error Interno: ${e.getMessage}"))
    }
  }

  // ─── ServerEndpoints con UserId inyectado desde el JWT ───

  val endpoints: List[ServerEndpoint[Any, IO]] = List(
    sendTextEndpoint.serverLogic(userId => req => executeLogic(req.toCommand(userId))),
    sendImageEndpoint.serverLogic(userId => req => executeLogic(req.toCommand(userId))),
    sendVideoEndpoint.serverLogic(userId => req => executeLogic(req.toCommand(userId))),
    sendAudioEndpoint.serverLogic(userId => req => executeLogic(req.toCommand(userId))),
    sendStickerEndpoint.serverLogic(userId => req => executeLogic(req.toCommand(userId))),
    sendDocumentEndpoint.serverLogic(userId => req => executeLogic(req.toCommand(userId))),
    getHistoryEndpoint.serverLogic(_userId => { case (roomId, limitOpt, offsetOpt) =>
      val limit = limitOpt.getOrElse(50)
      val offset = offsetOpt.getOrElse(0)
      messageRepository.findByRoomId(RoomId(roomId), limit, offset)
        .map(msgs => Right[String, List[MessageDto]](msgs.map(MessageDto.fromDomain)))
        .handleErrorWith(e => IO.pure(Left(s"Error consultando historial: ${e.getMessage}")))
    })
  )
}

