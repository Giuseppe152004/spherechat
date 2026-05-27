package com.proyecto.api.auth.application.port.out

import cats.effect.IO
import com.proyecto.api.auth.domain.model.AuthUser

trait AuthUserRepository {
  def findByUsername(username: String): IO[Option[AuthUser]]
}
