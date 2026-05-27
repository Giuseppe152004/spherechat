package com.proyecto.api.auth.infrastructure.adapter.in.http

import cats.effect.IO
import sttp.tapir.*
import sttp.tapir.generic.auto.*
import sttp.tapir.json.circe.*
import sttp.tapir.server.ServerEndpoint
import io.circe.generic.auto.*
import com.proyecto.api.auth.domain.model.Credentials
import com.proyecto.api.auth.application.service.LoginService

case class LoginRequest(username: String, passwordRaw: String) {
  def toDomain: Credentials = Credentials(username, passwordRaw)
}
case class LoginResponse(token: String)

class AuthEndpoints(loginService: LoginService) {

  val loginEndpoint: PublicEndpoint[LoginRequest, String, LoginResponse, Any] =
    endpoint.post
      .in("api" / "v1" / "auth" / "login")
      .in(jsonBody[LoginRequest].description("Credenciales de usuario para Login"))
      .out(jsonBody[LoginResponse])
      .errorOut(stringBody) // HTTP 400 default
      .description("Inicia sesión, verifica contraseña Bcrypt y obtiene un JWT")

  val loginServerEndpoint: ServerEndpoint[Any, IO] =
    loginEndpoint.serverLogic { req =>
      loginService.login(req.toDomain).map {
        case Right(token) => Right(LoginResponse(token))
        case Left(error)  => Left(error)
      }.handleErrorWith { err =>
        IO.pure(Left(s"Excepción Interna: ${err.getMessage}"))
      }
    }

  val endpoints: List[ServerEndpoint[Any, IO]] = List(loginServerEndpoint)
}
