package com.proyecto.api.chat.application.port.in

import cats.effect.IO

// DTO de Comando para el Caso de Uso (independiente de HTTP)
case class SendMessageCommand(
  roomId: Long,
  senderId: Long,
  messageType: Short,
  text: Option[String],
  url: Option[String],
  durationSeconds: Option[Double],
  sizeBytes: Option[Long],
  caption: Option[String],
  thumb: Option[String],
  waveform: Option[String],
  stickerId: Option[String],
  fileName: Option[String] = None,
  fileExtension: Option[String] = None
)

trait SendMessageUseCase {
  def execute(command: SendMessageCommand): IO[Long]
}
