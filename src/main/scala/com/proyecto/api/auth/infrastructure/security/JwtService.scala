package com.proyecto.api.auth.infrastructure.security

import cats.effect.IO
import com.auth0.jwt.JWT
import com.auth0.jwt.algorithms.Algorithm
import java.util.Date
import java.time.Instant
import scala.util.Try

class JwtService(secret: String) {
  private val algorithm = Algorithm.HMAC256(secret)
  private val verifier = JWT.require(algorithm).build()

  def generateToken(userId: Long): IO[String] = IO.delay {
    val expiresAt = Date.from(Instant.now().plusSeconds(86400)) // 24 horas
    JWT.create()
      .withClaim("userId", userId)
      .withExpiresAt(expiresAt)
      .sign(algorithm)
  }

  def validateToken(token: String): IO[Either[String, Long]] = IO.delay {
    Try {
      val decoded = verifier.verify(token)
      val userId = decoded.getClaim("userId").asLong()
      if (userId == null) throw new RuntimeException("Falta el claim userId en el Token")
      userId.toLong
    }.toEither.left.map(err => s"Token inválido o expirado: ${err.getMessage}")
  }
}
