package com.proyecto.api.chat.application.service

import cats.effect.IO
import com.proyecto.api.chat.application.port.in.{SendMessageUseCase, SendMessageCommand}
import com.proyecto.api.chat.application.port.out.MessageRepository
import com.proyecto.api.chat.domain.model._

class SendMessageService(messageRepository: MessageRepository) extends SendMessageUseCase {

  override def execute(command: SendMessageCommand): IO[Long] = {
    for {
      createdAt <- IO.realTimeInstant
      
      content <- buildContent(command)
      
      // ID es 0 temporalmente, la BD lo ignorará y asignará uno real vía RETURNING
      message = Message(
        id = MessageId(0L), 
        roomId = RoomId(command.roomId),
        senderId = UserId(command.senderId),
        messageType = MessageType.fromCode(command.messageType),
        content = content,
        createdAt = createdAt,
        isRead = false
      )
      
      // El repositorio devuelve la entidad reconstruida con el ID generado real
      savedMessage <- messageRepository.save(message)
      
    } yield savedMessage.id.value
  }

  private def buildContent(cmd: SendMessageCommand): IO[MessageContent] = IO.delay {
    MessageType.fromCode(cmd.messageType) match {
      case MessageType.TEXT => 
        MessageContent.TextContent(cmd.text.getOrElse(throw new IllegalArgumentException("Text is required for TEXT")))
      case MessageType.AUDIO => 
        MessageContent.AudioContent(
          cmd.url.getOrElse(throw new IllegalArgumentException("URL is required")),
          cmd.durationSeconds.getOrElse(throw new IllegalArgumentException("Duration is required")),
          cmd.waveform
        )
      case MessageType.PHOTO =>
        MessageContent.PhotoContent(
          cmd.url.getOrElse(throw new IllegalArgumentException("URL is required")),
          cmd.sizeBytes,
          cmd.caption
        )
      case MessageType.VIDEO =>
        MessageContent.VideoContent(
          cmd.url.getOrElse(throw new IllegalArgumentException("URL is required")),
          cmd.durationSeconds.getOrElse(0.0),
          cmd.sizeBytes,
          cmd.thumb
        )
      case MessageType.STICKER => 
        MessageContent.StickerContent(cmd.stickerId.getOrElse(throw new IllegalArgumentException("StickerId is required")))
      case MessageType.DOCUMENT =>
        MessageContent.DocumentContent(
          cmd.url.getOrElse(throw new IllegalArgumentException("URL is required for DOCUMENT")),
          cmd.fileName.getOrElse(throw new IllegalArgumentException("fileName is required for DOCUMENT")),
          cmd.fileExtension.getOrElse(throw new IllegalArgumentException("fileExtension is required for DOCUMENT")),
          cmd.sizeBytes.getOrElse(throw new IllegalArgumentException("sizeBytes is required for DOCUMENT"))
        )
    }
  }
}
