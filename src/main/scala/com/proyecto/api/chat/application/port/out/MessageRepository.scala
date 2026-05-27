package com.proyecto.api.chat.application.port.out

import cats.effect.IO
import com.proyecto.api.chat.domain.model.{Message, RoomId}

/**
 * Contrato funcional de persistencia.
 */
trait MessageRepository {
  
  // Retorna el mensaje reconstruido con el ID asignado por la BD (RETURNING)
  def save(message: Message): IO[Message]
  
  def findByRoomId(roomId: RoomId, limit: Int, offset: Int): IO[List[Message]]
  
}
