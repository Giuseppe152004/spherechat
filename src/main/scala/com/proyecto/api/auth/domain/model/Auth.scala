package com.proyecto.api.auth.domain.model

case class Credentials(username: String, passwordRaw: String)
case class AuthUser(id: Long, username: String, passwordHash: String)
