package com.proyecto.api.chat.infrastructure.adapter.out.postgres

import cats.effect._
import cats.implicits._
import skunk._
import skunk.implicits._
import skunk.codec.all._
import com.proyecto.api.chat.domain.model._
import com.proyecto.api.chat.application.port.out.MessageRepository

import java.time.{OffsetDateTime, ZoneOffset, Instant, LocalDateTime}

/**
 * Adaptador de Persistencia hacia `message.history` usando Skunk.
 * Mapeo plano sin JSONB, desarmando el ADT MessageContent en columnas opcionales.
 */
class SkunkMessageRepository(sessionPool: Resource[IO, Session[IO]]) extends MessageRepository {

  // --- Codecs de Tipos Base ---
  private val messageIdCodec: Codec[MessageId] = int8.imap(MessageId.apply)(_.value)
  private val roomIdCodec:    Codec[RoomId]    = int8.imap(RoomId.apply)(_.value)
  private val userIdCodec:    Codec[UserId]    = int8.imap(UserId.apply)(_.value)
  private val typeCodec:      Codec[MessageType] = int2.imap(MessageType.fromCode)(_.code)
  
  private val instantCodec: Codec[Instant] = 
    timestamp.imap(ts => ts.toInstant(ZoneOffset.UTC))(inst => LocalDateTime.ofInstant(inst, ZoneOffset.UTC))

  // --- Mapeo Plano para INSERT (Excluyendo ID que es Identity) ---
  private val insertQuery: Query[Message, (MessageId, Instant)] =
    sql"""
      INSERT INTO "message"."history" (
        room_id, sender_id, type, content, 
        attachment_url, attachment_type, attachment_size, attachment_duration, attachment_thumb, attachment_waveform
      )
      VALUES (
        ${roomIdCodec}, ${userIdCodec}, ${typeCodec}, ${text.opt}, 
        ${text.opt}, ${text.opt}, ${int8.opt}, ${float8.opt}, ${text.opt}, ${text.opt}
      )
      RETURNING id, sent_at
    """.query(messageIdCodec ~ instantCodec)
       .contramap { msg =>
         // Flat Mapping: Descomponemos el ADT en columnas opcionales
         val (contentOpt, url, attType, size, dur, thumb, wave) = msg.content match {
           case MessageContent.TextContent(t) => 
             (Some(t), None, None, None, None, None, None)
           case MessageContent.PhotoContent(u, s, c) => 
             (c, Some(u), Some("image"), s, None, None, None)
           case MessageContent.VideoContent(u, d, s, th) =>
             (None, Some(u), Some("video"), s, Some(d), th, None)
           case MessageContent.AudioContent(u, d, w) =>
             (None, Some(u), Some("audio"), None, Some(d), None, w)
           case MessageContent.StickerContent(st) =>
             (Some(st), None, Some("sticker"), None, None, None, None)
           case MessageContent.DocumentContent(u, fn, ext, sz) =>
             (Some(s"$fn.$ext"), Some(u), Some("document"), Some(sz), None, None, None)
         }
         
         // En Skunk 0.6+ con Scala 3, los Codecs combinados requieren tuplas planas nativas
         (msg.roomId, msg.senderId, msg.messageType, contentOpt, url, attType, size, dur, thumb, wave)
       }

  // --- Mapeo para LECTURA (Reconstrucción del ADT desde columnas planas) ---
  private val selectCodec: Codec[Message] = (
    messageIdCodec ~ roomIdCodec ~ userIdCodec ~ typeCodec ~ text.opt ~ instantCodec ~ 
    text.opt ~ int8.opt ~ float8.opt ~ text.opt ~ text.opt ~ int2.opt
  ).imap {
    case id ~ rId ~ sId ~ mCode ~ contentOpt ~ sentAt ~ attUrl ~ attSize ~ attDur ~ attThumb ~ attWave ~ statusOpt =>
      
      val content = mCode match {
        case MessageType.TEXT => 
          MessageContent.TextContent(contentOpt.getOrElse(""))
        case MessageType.PHOTO => 
          MessageContent.PhotoContent(attUrl.getOrElse(""), attSize, contentOpt)
        case MessageType.VIDEO => 
          MessageContent.VideoContent(attUrl.getOrElse(""), attDur.getOrElse(0.0), attSize, attThumb)
        case MessageType.AUDIO => 
          MessageContent.AudioContent(attUrl.getOrElse(""), attDur.getOrElse(0.0), attWave)
        case MessageType.STICKER => 
          MessageContent.StickerContent(contentOpt.getOrElse(""))
        case MessageType.DOCUMENT =>
          val fullName = contentOpt.getOrElse("unknown.bin")
          val dotIdx = fullName.lastIndexOf('.')
          val (name, ext) = if (dotIdx > 0) (fullName.substring(0, dotIdx), fullName.substring(dotIdx + 1)) else (fullName, "")
          MessageContent.DocumentContent(attUrl.getOrElse(""), name, ext, attSize.getOrElse(0L))
      }
      
      // status 2 = leido, o adaptado según negocio
      val isRead = statusOpt.getOrElse(1.toShort) == 2 
      Message(id, rId, sId, mCode, content, sentAt, isRead)
  } { msg =>
      throw new UnsupportedOperationException("Solo usado para decodificar consultas SELECT")
  }

  private val selectByRoomIdQuery: Query[(RoomId, Int, Int), Message] =
    sql"""
      SELECT id, room_id, sender_id, type, content, sent_at, 
             attachment_url, attachment_size, attachment_duration, attachment_thumb, attachment_waveform, status
      FROM "message"."history"
      WHERE room_id = $roomIdCodec
      ORDER BY sent_at DESC
      LIMIT $int4
      OFFSET $int4
    """.query(selectCodec)


  override def save(message: Message): IO[Message] = {
    sessionPool.use { session =>
      session.prepare(insertQuery).flatMap { query =>
        query.unique(message).map { case (genId, genDate) =>
          // 4. Hidratar la entidad de Dominio con los datos reales autogenerados por PG
          message.copy(id = genId, createdAt = genDate)
        }
      }
    }.handleErrorWith { err =>
      IO.raiseError(new RuntimeException(s"[DB Error] Fallo al insertar mensaje: ${err.getMessage}", err))
    }
  }

  override def findByRoomId(roomId: RoomId, limit: Int, offset: Int): IO[List[Message]] = {
    sessionPool.use { session =>
      session.prepare(selectByRoomIdQuery).flatMap { query =>
        query.stream((roomId, limit, offset), 64).compile.toList
      }
    }.handleErrorWith { err =>
      IO.raiseError(new RuntimeException(s"[DB Error] Fallo consultando historial: ${err.getMessage}", err))
    }
  }
}
