package com.proyecto.api.chat.application.service

import cats.effect.IO
import munit.CatsEffectSuite
import com.proyecto.api.chat.application.port.in.SendMessageCommand
import com.proyecto.api.chat.application.port.out.MessageRepository
import com.proyecto.api.chat.domain.model._
import java.time.Instant

/**
 * Suite de pruebas para la capa de Aplicación (SendMessageService).
 * 
 * Estrategia:
 *   - Usamos un Mock funcional puro del MessageRepository (sin librerías externas).
 *   - Validamos que el servicio orquesta correctamente: construir contenido -> persistir -> retornar ID.
 *   - Validamos los escenarios de fallo cuando faltan campos obligatorios.
 *   - Validamos que el repositorio con error propaga correctamente la excepción.
 */
class SendMessageServiceSuite extends CatsEffectSuite {

  // ═══════════════════════════════════════════════════════════════
  // Mock Funcional Puro del Repositorio
  // ═══════════════════════════════════════════════════════════════

  /** Mock que simula un save exitoso, devolviendo el mensaje con ID = 42 */
  private val successRepo = new MessageRepository {
    override def save(message: Message): IO[Message] = IO.pure(
      message.copy(id = MessageId(42L), createdAt = Instant.parse("2026-01-01T00:00:00Z"))
    )
    override def findByRoomId(roomId: RoomId, limit: Int, offset: Int): IO[List[Message]] = IO.pure(Nil)
  }

  /** Mock que simula un fallo de la base de datos */
  private val failingRepo = new MessageRepository {
    override def save(message: Message): IO[Message] = 
      IO.raiseError(new RuntimeException("Conexión con BD perdida"))
    override def findByRoomId(roomId: RoomId, limit: Int, offset: Int): IO[List[Message]] = IO.pure(Nil)
  }

  // ═══════════════════════════════════════════════════════════════
  // Happy Path — Cada tipo de mensaje
  // ═══════════════════════════════════════════════════════════════

  test("execute con TEXT devuelve el ID generado por el repositorio") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 1, Some("Hola"), None, None, None, None, None, None, None)
    service.execute(cmd).map(id => assertEquals(id, 42L))
  }

  test("execute con PHOTO devuelve el ID generado por el repositorio") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 2, None, Some("https://img.url"), None, Some(1024L), Some("Foto"), None, None, None)
    service.execute(cmd).map(id => assertEquals(id, 42L))
  }

  test("execute con VIDEO devuelve el ID generado por el repositorio") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 3, None, Some("https://vid.url"), Some(60.0), Some(5000000L), None, Some("thumb"), None, None)
    service.execute(cmd).map(id => assertEquals(id, 42L))
  }

  test("execute con AUDIO devuelve el ID generado por el repositorio") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 4, None, Some("https://aud.url"), Some(15.5), None, None, None, Some("waveform_data"), None)
    service.execute(cmd).map(id => assertEquals(id, 42L))
  }

  test("execute con STICKER devuelve el ID generado por el repositorio") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 5, None, None, None, None, None, None, None, Some("STK-001"))
    service.execute(cmd).map(id => assertEquals(id, 42L))
  }

  test("execute con DOCUMENT devuelve el ID generado por el repositorio") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 6, None, Some("https://doc.url"), None, Some(512000L), None, None, None, None, Some("reporte"), Some("pdf"))
    service.execute(cmd).map(id => assertEquals(id, 42L))
  }

  // ═══════════════════════════════════════════════════════════════
  // Escenarios de Fallo — Campos obligatorios faltantes
  // ═══════════════════════════════════════════════════════════════

  test("execute con TEXT sin campo text lanza IllegalArgumentException") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 1, None, None, None, None, None, None, None, None)
    interceptIO[IllegalArgumentException](service.execute(cmd))
  }

  test("execute con PHOTO sin campo url lanza IllegalArgumentException") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 2, None, None, None, None, None, None, None, None)
    interceptIO[IllegalArgumentException](service.execute(cmd))
  }

  test("execute con AUDIO sin campo url lanza IllegalArgumentException") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 4, None, None, None, None, None, None, None, None)
    interceptIO[IllegalArgumentException](service.execute(cmd))
  }

  test("execute con AUDIO sin campo durationSeconds lanza IllegalArgumentException") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 4, None, Some("https://aud.url"), None, None, None, None, None, None)
    interceptIO[IllegalArgumentException](service.execute(cmd))
  }

  test("execute con STICKER sin stickerId lanza IllegalArgumentException") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 5, None, None, None, None, None, None, None, None)
    interceptIO[IllegalArgumentException](service.execute(cmd))
  }

  test("execute con DOCUMENT sin url lanza IllegalArgumentException") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 6, None, None, None, Some(100L), None, None, None, None, Some("file"), Some("pdf"))
    interceptIO[IllegalArgumentException](service.execute(cmd))
  }

  test("execute con DOCUMENT sin fileName lanza IllegalArgumentException") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 6, None, Some("url"), None, Some(100L), None, None, None, None, None, Some("pdf"))
    interceptIO[IllegalArgumentException](service.execute(cmd))
  }

  test("execute con DOCUMENT sin sizeBytes lanza IllegalArgumentException") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 6, None, Some("url"), None, None, None, None, None, None, Some("file"), Some("pdf"))
    interceptIO[IllegalArgumentException](service.execute(cmd))
  }

  // ═══════════════════════════════════════════════════════════════
  // Escenario Catastrófico — Error de base de datos
  // ═══════════════════════════════════════════════════════════════

  test("execute propaga RuntimeException si el repositorio falla al guardar") {
    val service = new SendMessageService(failingRepo)
    val cmd = SendMessageCommand(1L, 10L, 1, Some("Hola"), None, None, None, None, None, None, None)
    interceptIO[RuntimeException](service.execute(cmd))
  }

  // ═══════════════════════════════════════════════════════════════
  // Código desconocido — Fallback a TEXT
  // ═══════════════════════════════════════════════════════════════

  test("execute con messageType desconocido (99) trata como TEXT si se provee text") {
    val service = new SendMessageService(successRepo)
    val cmd = SendMessageCommand(1L, 10L, 99, Some("fallback text"), None, None, None, None, None, None, None)
    service.execute(cmd).map(id => assertEquals(id, 42L))
  }
}
