package com.proyecto.api

import cats.effect.{IO, IOApp, Resource}
import cats.syntax.all.*
import com.comcast.ip4s.*
import org.http4s.ember.server.EmberServerBuilder
import sttp.tapir.server.http4s.Http4sServerInterpreter
import com.proyecto.api.infrastructure.adapter.in.http.auth.AuthEndpoints
import com.proyecto.api.infrastructure.adapter.in.http.capacitacion.CapacitacionEndpoints
import com.proyecto.api.infrastructure.adapter.in.http.ScalarDocsRoutes
import com.proyecto.api.infrastructure.adapter.out.db.*
import com.proyecto.api.infrastructure.adapter.out.security.JwtTokenService
import com.proyecto.api.application.port.out.*
import com.proyecto.api.domain.model.*
import com.proyecto.api.domain.error.AuthError
import com.proyecto.api.application.service.{AuthService, CapacitacionService}

// -- Importaciones para el sistema de Chat (Skunk + Http4s puros) --
import skunk.Session
import natchez.Trace.Implicits.noop
import com.proyecto.api.chat.infrastructure.adapter.out.postgres.SkunkMessageRepository
import com.proyecto.api.chat.application.service.SendMessageService
import com.proyecto.api.chat.infrastructure.adapter.in.http.ChatEndpoints

object Main extends IOApp.Simple:

  // 1. Servicios de utilidad y firma de Tokens
  val jwtSecret = sys.env.getOrElse("JWT_SECRET", "mi-api-scala-secreto-desarrollo-2026")
  val tokenService = new JwtTokenService(jwtSecret)

  val dummyApiKeyValidator = new ApiKeyValidator[IO]:
    def isValid(apiKey: String): IO[Boolean] = IO.pure(apiKey == "secret-api-key")

  // 2. Instanciar Transactor y Repositorios reales de PostgreSQL
  val postgresTransactor = new PostgresTransactor()
  val realUserRepository = new PostgresUserRepository(postgresTransactor)
  val realCapacitacionRepository = new PostgresCapacitacionRepository(postgresTransactor)

  // 3. Instanciar Servicios (Legacy)
  val authService = new AuthService[IO](realUserRepository, tokenService)
  val capacitacionService = new CapacitacionService[IO](realCapacitacionRepository)

  // 4. Instanciar Endpoints (Tapir Legacy)
  val authEndpoints = new AuthEndpoints(authService, dummyApiKeyValidator, tokenService)
  val capacitacionEndpoints = new CapacitacionEndpoints(capacitacionService, dummyApiKeyValidator, tokenService)

  val apiServerEndpoints = List(
    authEndpoints.loginEndpoint,
    authEndpoints.protectedHelloEndpoint
  ) ++ capacitacionEndpoints.endpoints

  val tapirRoutes = Http4sServerInterpreter[IO]().toRoutes(apiServerEndpoints)

  val docsRoutes = ScalarDocsRoutes.routes(
    apiServerEndpoints.map(_.endpoint),
    "API Autenticación Híbrida y Chat", "1.0"
  )

  // -- Configuración Skunk para el módulo de Chat (Pool concurrente) --
  val sessionPool: Resource[IO, Resource[IO, Session[IO]]] =
    Session.pooled[IO](
      host = "10.10.40.247",
      port = 5432,
      user = "patricia",
      database = "sphere",
      password = Some("123456"),
      max = 10
    )

  // 5. Arranque principal
  def run: IO[Unit] = 
    sessionPool.use { pool =>
      
      // --- Módulo Auth Funcional ---
      val jwtService = new com.proyecto.api.auth.infrastructure.security.JwtService("mi-api-scala-secreto-desarrollo-2026")
      val skunkAuthUserRepository = new com.proyecto.api.auth.infrastructure.adapter.out.postgres.SkunkAuthUserRepository(pool)
      val functionalLoginService = new com.proyecto.api.auth.application.service.LoginService(skunkAuthUserRepository, jwtService)
      val functionalAuthEndpoints = new com.proyecto.api.auth.infrastructure.adapter.in.http.AuthEndpoints(functionalLoginService)

      // --- Módulo Chat Funcional ---
      val messageRepository = new SkunkMessageRepository(pool)
      val sendMessageUseCase = new SendMessageService(messageRepository)
      val chatEndpoints = new ChatEndpoints(sendMessageUseCase, messageRepository, jwtService)

      // --- Módulo de Upload y Archivos Estáticos ---
      val uploadDir = java.nio.file.Paths.get("C:\\spherechat_uploads")
      val publicBaseUrl = "http://localhost:8082"
      val fileUploadRoutes = new com.proyecto.api.chat.infrastructure.adapter.in.http.FileUploadRoutes(jwtService, uploadDir, publicBaseUrl)

      // 1. Rutas y Docs Legacy (Autenticación, Capacitación)
      val legacyTapirRoutes = Http4sServerInterpreter[IO]().toRoutes(apiServerEndpoints)
      val legacyDocsRoutes = ScalarDocsRoutes.routes(
        apiServerEndpoints.map(_.endpoint),
        "API Core (Auth & Capacitación)", "1.0",
        docsPath = "docs", openApiPath = "openapi.json"
      )

      // 2. Rutas y Docs Funcionales (Chat + Login Funcional)
      val functionalServerEndpoints = functionalAuthEndpoints.endpoints ::: chatEndpoints.endpoints
      val chatTapirRoutes = Http4sServerInterpreter[IO]().toRoutes(functionalServerEndpoints)
      val chatDocsRoutes = ScalarDocsRoutes.routes(
        functionalServerEndpoints.map(_.endpoint),
        "API Mensajería (Chat) y Seguridad", "1.0",
        docsPath = "chat-docs", openApiPath = "chat-openapi.json"
      )

      // 3. Composición final: Tapir + Upload Http4s + Static Files
      val allRoutes = legacyTapirRoutes <+> legacyDocsRoutes <+> chatTapirRoutes <+> chatDocsRoutes <+> fileUploadRoutes.routes

      val serverResource = EmberServerBuilder
        .default[IO]
        .withHost(ipv4"0.0.0.0")
        .withPort(port"8082")
        .withHttpApp(allRoutes.orNotFound)
        .withMaxHeaderSize(32768) // Headers más grandes para tokens JWT
        .build

      serverResource.use { server =>
        IO.println(s"✅ Servidor levantado con éxito. Documentación Scalar en: http://localhost:${server.address.getPort}/docs") *>
        IO.println(s"🚀 Módulo de Chat integrado con Skunk Pool conectado a PostgreSQL.") *>
        IO.println(s"📁 Archivos estáticos servidos desde: ${uploadDir.toAbsolutePath}") *>
        IO.println(s"📤 Endpoint de Upload: POST http://localhost:${server.address.getPort}/api/v1/chat/upload") *>
        IO.println(s"📜 Historial: GET http://localhost:${server.address.getPort}/api/v1/chat/rooms/{roomId}/messages") *>
        IO.never
      }
    }