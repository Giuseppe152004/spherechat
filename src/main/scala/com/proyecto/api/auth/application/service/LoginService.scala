package com.proyecto.api.auth.application.service

import cats.effect.IO
import at.favre.lib.crypto.bcrypt.BCrypt
import com.proyecto.api.auth.domain.model.Credentials
import com.proyecto.api.auth.application.port.out.AuthUserRepository
import com.proyecto.api.auth.infrastructure.security.JwtService

class LoginService(userRepo: AuthUserRepository, jwtService: JwtService) {
  def login(credentials: Credentials): IO[Either[String, String]] = {
    userRepo.findByUsername(credentials.username).flatMap {
      case Some(user) =>
        IO.blocking(BCrypt.verifyer().verify(credentials.passwordRaw.toCharArray, user.passwordHash).verified).flatMap { isValid =>
          if (isValid) jwtService.generateToken(user.id).map(Right(_))
          else IO.pure(Left("Credenciales inválidas"))
        }
      case None =>
        IO.pure(Left("Credenciales inválidas")) // Mismo mensaje de error por seguridad
    }
  }
}
