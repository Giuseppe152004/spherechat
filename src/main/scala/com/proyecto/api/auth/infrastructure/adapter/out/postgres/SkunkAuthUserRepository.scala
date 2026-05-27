package com.proyecto.api.auth.infrastructure.adapter.out.postgres

import cats.effect._
import cats.implicits._
import skunk._
import skunk.implicits._
import skunk.codec.all._
import com.proyecto.api.auth.domain.model.AuthUser
import com.proyecto.api.auth.application.port.out.AuthUserRepository

class SkunkAuthUserRepository(sessionPool: Resource[IO, Session[IO]]) extends AuthUserRepository {

  private val userCodec: Codec[AuthUser] = (int8 ~ text ~ text).imap {
    case id ~ username ~ hash => AuthUser(id, username, hash)
  }(u => u.id ~ u.username ~ u.passwordHash)

  private val findQuery: Query[String, AuthUser] =
    sql"""
      SELECT id, username::text, password_hash
      FROM "credentials"."users"
      WHERE username = $text
      LIMIT 1
    """.query(userCodec)

  override def findByUsername(username: String): IO[Option[AuthUser]] = {
    sessionPool.use { session =>
      session.prepare(findQuery).flatMap { query =>
        query.option(username)
      }
    }.handleErrorWith { err =>
      IO.raiseError(new RuntimeException(s"Error buscando usuario en BD: ${err.getMessage}"))
    }
  }
}
