package com.proyecto.api.chat.infrastructure.adapter.in.http

import cats.effect.IO
import cats.syntax.all.*
import org.http4s.*
import org.http4s.dsl.io.*
import org.http4s.multipart.Multipart
import org.http4s.headers.`Content-Type`
import io.circe.syntax.*
import io.circe.generic.auto.*
import org.http4s.circe.CirceEntityEncoder.*
import com.proyecto.api.auth.infrastructure.security.JwtService

import java.nio.file.{Files, Path, Paths}
import java.util.UUID

/**
 * Respuesta del endpoint de upload.
 * El Frontend usará esta URL para enviarla a los endpoints de mensajes.
 */
case class UploadResponse(
  url: String,
  fileName: String,
  extension: String,
  sizeBytes: Long
)

/**
 * Rutas Http4s puras para:
 *   1. POST /api/v1/chat/upload  → Carga de archivos multipart (protegido por JWT)
 *   2. GET  /uploads/{filename}  → Servir archivos estáticos subidos
 *
 * Patrón "Upload & Notify":
 *   El cliente sube el archivo aquí, recibe la URL pública,
 *   y luego la envía a POST /messages/video, /messages/image, etc.
 */
class FileUploadRoutes(jwtService: JwtService, uploadDir: Path, publicBaseUrl: String) {

  // Asegurar que el directorio de uploads existe al iniciar
  Files.createDirectories(uploadDir)

  // Extensiones permitidas para evitar subir ejecutables maliciosos
  private val allowedExtensions = Set(
    // Imágenes
    "jpg", "jpeg", "png", "gif", "webp", "bmp", "svg",
    // Videos
    "mp4", "mov", "avi", "mkv", "webm",
    // Audios
    "mp3", "ogg", "wav", "aac", "m4a", "opus",
    // Documentos
    "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "csv", "zip", "rar"
  )

  /**
   * Extrae y valida el Bearer Token del header Authorization.
   * Retorna el userId si es válido, o un Response 401 si no.
   */
  private def authenticate(req: Request[IO]): IO[Either[Response[IO], Long]] = {
    req.headers.get[headers.Authorization] match {
      case Some(authHeader) =>
        val token = authHeader.credentials.renderString.stripPrefix("Bearer ")
        jwtService.validateToken(token).map {
          case Right(userId) => Right(userId)
          case Left(error)   => Left(Response[IO](status = Status.Unauthorized).withEntity(error))
        }
      case None =>
        IO.pure(Left(Response[IO](status = Status.Unauthorized).withEntity("Token de autorización requerido")))
    }
  }

  /**
   * Endpoint de Upload: POST /api/v1/chat/upload
   * Acepta multipart/form-data con un campo "file".
   */
  val uploadRoutes: HttpRoutes[IO] = HttpRoutes.of[IO] {
    case req @ POST -> Root / "api" / "v1" / "chat" / "upload" =>
      authenticate(req).flatMap {
        case Left(errorResponse) => IO.pure(errorResponse)
        case Right(_userId) =>
          req.decode[Multipart[IO]] { multipart =>
            multipart.parts.find(_.name.contains("file")) match {
              case Some(filePart) =>
                val originalName = filePart.filename.getOrElse("unknown")
                val dotIdx = originalName.lastIndexOf('.')
                val extension = if (dotIdx > 0) originalName.substring(dotIdx + 1).toLowerCase else "bin"
                val baseName = if (dotIdx > 0) originalName.substring(0, dotIdx) else originalName

                if (!allowedExtensions.contains(extension)) {
                  BadRequest(s"Extensión '$extension' no permitida. Extensiones válidas: ${allowedExtensions.mkString(", ")}")
                } else {
                  // Generar nombre único para evitar colisiones
                  val uniqueName = s"${UUID.randomUUID()}_${baseName}.$extension"
                  val targetPath = uploadDir.resolve(uniqueName)

                  // Escribir bytes al disco de forma segura con fs2
                  filePart.body
                    .through(fs2.io.file.Files[IO].writeAll(fs2.io.file.Path.fromNioPath(targetPath)))
                    .compile
                    .drain
                    .flatMap { _ =>
                      val fileSize = Files.size(targetPath)
                      val publicUrl = s"$publicBaseUrl/uploads/$uniqueName"

                      Ok(UploadResponse(
                        url = publicUrl,
                        fileName = baseName,
                        extension = extension,
                        sizeBytes = fileSize
                      ))
                    }
                    .handleErrorWith { err =>
                      InternalServerError(s"Error al guardar archivo: ${err.getMessage}")
                    }
                }

              case None =>
                BadRequest("No se encontró el campo 'file' en el formulario multipart. Envía el archivo con el nombre 'file'.")
            }
          }
      }
  }

  /**
   * Servidor de archivos estáticos: GET /uploads/{filename}
   * Permite al navegador o cliente C# hacer streaming directo de los archivos.
   */
  val staticRoutes: HttpRoutes[IO] = HttpRoutes.of[IO] {
    case req @ GET -> Root / "uploads" / fileName =>
      val filePath = uploadDir.resolve(fileName)

      // Seguridad: Evitar path traversal (../)
      if (!filePath.normalize().startsWith(uploadDir.normalize())) {
        Forbidden("Acceso denegado")
      } else if (!Files.exists(filePath)) {
        NotFound(s"Archivo '$fileName' no encontrado")
      } else {
        // Detectar Content-Type automáticamente
        val mediaType = fileName.split('.').lastOption.map(_.toLowerCase) match {
          case Some("jpg") | Some("jpeg") => MediaType.image.jpeg
          case Some("png")                => MediaType.image.png
          case Some("gif")                => MediaType.image.gif
          case Some("webp")               => MediaType.image.webp
          case Some("mp4")                => MediaType.video.mp4
          case Some("webm")               => MediaType.video.webm
          case Some("mp3")                => MediaType.audio.mpeg
          case Some("ogg")                => MediaType.audio.ogg
          case Some("wav")                => MediaType.audio.wav
          case Some("pdf")                => MediaType.application.pdf
          case _                          => MediaType.application.`octet-stream`
        }

        StaticFile.fromPath(
          fs2.io.file.Path.fromNioPath(filePath), 
          Some(req)
        ).getOrElseF(NotFound(s"Archivo '$fileName' no encontrado"))
      }
  }

  /** Todas las rutas combinadas para montar en Main */
  val routes: HttpRoutes[IO] = uploadRoutes <+> staticRoutes
}
