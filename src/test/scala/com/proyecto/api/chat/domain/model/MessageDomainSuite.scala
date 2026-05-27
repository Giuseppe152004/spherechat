package com.proyecto.api.chat.domain.model

import munit.CatsEffectSuite
import java.time.Instant

/**
 * Suite de pruebas unitarias para la capa de Dominio del módulo de Chat.
 * 
 * Estrategia de Pruebas:
 *   - Validar el enum MessageType y su factory fromCode (happy path + código desconocido)
 *   - Validar la construcción correcta de cada variante del ADT MessageContent
 *   - Validar la entidad raíz Message con distintos tipos de contenido
 * 
 * Patrón: AAA (Arrange-Act-Assert)
 * Dependencias externas: NINGUNA (pruebas puramente funcionales)
 */
class MessageDomainSuite extends CatsEffectSuite {

  // ═══════════════════════════════════════════════════════════════
  // MessageType — Enum Mapping
  // ═══════════════════════════════════════════════════════════════

  test("MessageType.fromCode devuelve TEXT para code 1") {
    assertEquals(MessageType.fromCode(1), MessageType.TEXT)
  }

  test("MessageType.fromCode devuelve PHOTO para code 2") {
    assertEquals(MessageType.fromCode(2), MessageType.PHOTO)
  }

  test("MessageType.fromCode devuelve VIDEO para code 3") {
    assertEquals(MessageType.fromCode(3), MessageType.VIDEO)
  }

  test("MessageType.fromCode devuelve AUDIO para code 4") {
    assertEquals(MessageType.fromCode(4), MessageType.AUDIO)
  }

  test("MessageType.fromCode devuelve STICKER para code 5") {
    assertEquals(MessageType.fromCode(5), MessageType.STICKER)
  }

  test("MessageType.fromCode devuelve DOCUMENT para code 6") {
    assertEquals(MessageType.fromCode(6), MessageType.DOCUMENT)
  }

  test("MessageType.fromCode retorna TEXT como fallback para código desconocido (99)") {
    assertEquals(MessageType.fromCode(99), MessageType.TEXT)
  }

  test("MessageType.fromCode retorna TEXT como fallback para código negativo (-1)") {
    assertEquals(MessageType.fromCode(-1), MessageType.TEXT)
  }

  // ═══════════════════════════════════════════════════════════════
  // MessageContent ADT — Construcción de variantes
  // ═══════════════════════════════════════════════════════════════

  test("TextContent almacena texto correctamente") {
    val content = MessageContent.TextContent("Hola mundo 🚀")
    assertEquals(content.text, "Hola mundo 🚀")
  }

  test("TextContent soporta cadenas vacías sin explotar") {
    val content = MessageContent.TextContent("")
    assertEquals(content.text, "")
  }

  test("TextContent soporta emojis complejos Unicode (skin tones, ZWJ sequences)") {
    val emoji = "👨‍👩‍👧‍👦🏳️‍🌈"
    val content = MessageContent.TextContent(emoji)
    assertEquals(content.text, emoji)
  }

  test("PhotoContent almacena URL, sizeBytes y caption correctamente") {
    val content = MessageContent.PhotoContent("https://cdn.test.com/img.jpg", Some(2048000L), Some("Foto bonita"))
    assertEquals(content.url, "https://cdn.test.com/img.jpg")
    assertEquals(content.sizeBytes, Some(2048000L))
    assertEquals(content.caption, Some("Foto bonita"))
  }

  test("PhotoContent permite sizeBytes y caption como None") {
    val content = MessageContent.PhotoContent("https://cdn.test.com/img.jpg")
    assertEquals(content.sizeBytes, None)
    assertEquals(content.caption, None)
  }

  test("VideoContent almacena todos los campos multimedia") {
    val content = MessageContent.VideoContent("https://cdn.test.com/vid.mp4", 120.5, Some(50000000L), Some("thumb_base64"))
    assertEquals(content.url, "https://cdn.test.com/vid.mp4")
    assertEquals(content.durationSeconds, 120.5)
    assertEquals(content.sizeBytes, Some(50000000L))
    assertEquals(content.thumb, Some("thumb_base64"))
  }

  test("AudioContent almacena URL, duración y waveform") {
    val content = MessageContent.AudioContent("https://cdn.test.com/audio.ogg", 35.2, Some("0.1,0.5,0.9"))
    assertEquals(content.url, "https://cdn.test.com/audio.ogg")
    assertEquals(content.durationSeconds, 35.2)
    assertEquals(content.waveform, Some("0.1,0.5,0.9"))
  }

  test("AudioContent permite waveform como None") {
    val content = MessageContent.AudioContent("https://cdn.test.com/audio.ogg", 10.0)
    assertEquals(content.waveform, None)
  }

  test("StickerContent almacena el stickerId") {
    val content = MessageContent.StickerContent("STK-GATO-001")
    assertEquals(content.stickerId, "STK-GATO-001")
  }

  test("DocumentContent almacena URL, fileName, extension y sizeBytes") {
    val content = MessageContent.DocumentContent("https://cdn.test.com/doc.pdf", "reporte_q3", "pdf", 2450000L)
    assertEquals(content.url, "https://cdn.test.com/doc.pdf")
    assertEquals(content.fileName, "reporte_q3")
    assertEquals(content.fileExtension, "pdf")
    assertEquals(content.sizeBytes, 2450000L)
  }

  // ═══════════════════════════════════════════════════════════════
  // Message (Entidad Raíz) — Integridad estructural
  // ═══════════════════════════════════════════════════════════════

  test("Message con TextContent se construye correctamente") {
    val msg = Message(
      id = MessageId(1L),
      roomId = RoomId(10L),
      senderId = UserId(42L),
      messageType = MessageType.TEXT,
      content = MessageContent.TextContent("Hola"),
      createdAt = Instant.parse("2026-05-26T12:00:00Z"),
      isRead = false
    )
    assertEquals(msg.id.value, 1L)
    assertEquals(msg.roomId.value, 10L)
    assertEquals(msg.senderId.value, 42L)
    assertEquals(msg.messageType, MessageType.TEXT)
    assertEquals(msg.isRead, false)
  }

  test("Message con DocumentContent se construye correctamente") {
    val msg = Message(
      id = MessageId(99L),
      roomId = RoomId(5L),
      senderId = UserId(7L),
      messageType = MessageType.DOCUMENT,
      content = MessageContent.DocumentContent("https://cdn.test.com/doc.xlsx", "presupuesto", "xlsx", 512000L),
      createdAt = Instant.now(),
      isRead = true
    )
    assertEquals(msg.messageType, MessageType.DOCUMENT)
    assertEquals(msg.isRead, true)
    assert(msg.content.isInstanceOf[MessageContent.DocumentContent])
  }

  test("Message.copy permite cambiar isRead sin afectar el contenido") {
    val original = Message(
      id = MessageId(1L), roomId = RoomId(1L), senderId = UserId(1L),
      messageType = MessageType.TEXT, content = MessageContent.TextContent("Test"),
      createdAt = Instant.now(), isRead = false
    )
    val updated = original.copy(isRead = true)
    assertEquals(updated.isRead, true)
    assertEquals(updated.content, original.content)
    assertEquals(updated.id, original.id)
  }

  // ═══════════════════════════════════════════════════════════════
  // Pattern Matching exhaustivo sobre el ADT (Polimorfismo)
  // ═══════════════════════════════════════════════════════════════

  test("Pattern matching sobre MessageContent cubre las 6 variantes del ADT") {
    val contents: List[MessageContent] = List(
      MessageContent.TextContent("hola"),
      MessageContent.PhotoContent("url", None, None),
      MessageContent.VideoContent("url", 10.0, None, None),
      MessageContent.AudioContent("url", 5.0, None),
      MessageContent.StickerContent("stk-1"),
      MessageContent.DocumentContent("url", "file", "pdf", 100L)
    )

    val labels = contents.map {
      case _: MessageContent.TextContent     => "text"
      case _: MessageContent.PhotoContent    => "photo"
      case _: MessageContent.VideoContent    => "video"
      case _: MessageContent.AudioContent    => "audio"
      case _: MessageContent.StickerContent  => "sticker"
      case _: MessageContent.DocumentContent => "document"
    }

    assertEquals(labels, List("text", "photo", "video", "audio", "sticker", "document"))
  }
}
