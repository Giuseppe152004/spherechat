package com.proyecto.api.chat.domain.model

import java.time.Instant

// Opaque Types para IDs basados en Long (asignados por BD relacional secuencial)
case class MessageId(value: Long) extends AnyVal
case class UserId(value: Long) extends AnyVal
case class RoomId(value: Long) extends AnyVal // Antes ChatId

// Discriminador de tipos mapeado a la columna `type` (int2)
enum MessageType(val code: Short) {
  case TEXT extends MessageType(1)
  case PHOTO extends MessageType(2)
  case VIDEO extends MessageType(3)
  case AUDIO extends MessageType(4)
  case STICKER extends MessageType(5)
  case DOCUMENT extends MessageType(6)
}

object MessageType {
  def fromCode(code: Short): MessageType = values.find(_.code == code).getOrElse(TEXT)
}

/**
 * Algebraic Data Type (ADT) sellado para los distintos contenidos.
 */
sealed trait MessageContent

object MessageContent {
  case class TextContent(text: String) extends MessageContent
  
  case class AudioContent(url: String, durationSeconds: Double, waveform: Option[String] = None) extends MessageContent
  
  // El caption de la foto irá en `content`, URL en `attachment_url`
  case class PhotoContent(url: String, sizeBytes: Option[Long] = None, caption: Option[String] = None) extends MessageContent
  
  case class VideoContent(url: String, durationSeconds: Double, sizeBytes: Option[Long] = None, thumb: Option[String] = None) extends MessageContent
  
  // Sticker ID irá en `content`
  case class StickerContent(stickerId: String) extends MessageContent
  
  // Documentos pesados (PDF, Word, Excel, etc.) — Patrón "Upload & Notify"
  // El archivo ya fue subido a un Storage externo; aquí solo recibimos los metadatos.
  case class DocumentContent(url: String, fileName: String, fileExtension: String, sizeBytes: Long) extends MessageContent
}

/**
 * Entidad Raíz
 */
case class Message(
  id: MessageId, // Puede inicializarse en 0 al crear antes de la BD
  roomId: RoomId,
  senderId: UserId,
  messageType: MessageType,
  content: MessageContent,
  createdAt: Instant,
  isRead: Boolean
)
