package com.proyecto.api.auth

import cats.effect.IO
import munit.CatsEffectSuite
import com.proyecto.api.auth.infrastructure.security.JwtService
import com.proyecto.api.auth.application.service.LoginService
import com.proyecto.api.auth.application.port.out.AuthUserRepository
import com.proyecto.api.auth.domain.model.{AuthUser, Credentials}
import at.favre.lib.crypto.bcrypt.BCrypt

/**
 * Suite de pruebas para el módulo de Autenticación.
 * 
 * Estrategia:
 *   - JwtService: Validar generación, verificación, tokens expirados y malformados.
 *   - LoginService: Validar login exitoso, contraseña incorrecta y usuario inexistente.
 *   - Mock funcional puro del AuthUserRepository (sin BD real).
 */
class AuthModuleSuite extends CatsEffectSuite {

  private val testSecret = "test-secret-key-for-unit-tests-2026"
  private val jwtService = new JwtService(testSecret)

  // ═══════════════════════════════════════════════════════════════
  // JwtService — Generación y Validación de Tokens
  // ═══════════════════════════════════════════════════════════════

  test("JwtService genera un token no vacío para un userId válido") {
    jwtService.generateToken(42L).map { token =>
      assert(token.nonEmpty, "El token no debe ser una cadena vacía")
      assert(token.contains("."), "El token JWT debe tener formato con puntos (header.payload.signature)")
    }
  }

  test("JwtService valida correctamente un token recién generado y extrae el userId") {
    for {
      token  <- jwtService.generateToken(99L)
      result <- jwtService.validateToken(token)
    } yield {
      assertEquals(result, Right(99L))
    }
  }

  test("JwtService devuelve Left para un token completamente inválido") {
    jwtService.validateToken("esto.no.es.un.jwt.valido").map { result =>
      assert(result.isLeft, "Un token basura debe devolver Left")
    }
  }

  test("JwtService devuelve Left para una cadena vacía") {
    jwtService.validateToken("").map { result =>
      assert(result.isLeft, "Un token vacío debe devolver Left")
    }
  }

  test("JwtService devuelve Left para un token firmado con otro secreto") {
    val otherJwtService = new JwtService("secreto-diferente-completamente-otro")
    for {
      token  <- otherJwtService.generateToken(42L)
      result <- jwtService.validateToken(token)
    } yield {
      assert(result.isLeft, "Un token firmado con otro secreto debe fallar la verificación")
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // LoginService — Flujo completo con Mock
  // ═══════════════════════════════════════════════════════════════

  // Generamos un hash BCrypt real para las pruebas
  private val realPasswordHash: String = BCrypt.withDefaults().hashToString(10, "mi_password_segura".toCharArray)

  private val mockUserRepo = new AuthUserRepository {
    override def findByUsername(username: String): IO[Option[AuthUser]] = IO.pure {
      if (username == "patricia") Some(AuthUser(1L, "patricia", realPasswordHash))
      else None
    }
  }

  private val loginService = new LoginService(mockUserRepo, jwtService)

  test("LoginService con credenciales correctas devuelve Right con un token JWT") {
    loginService.login(Credentials("patricia", "mi_password_segura")).map { result =>
      assert(result.isRight, s"Login debió ser exitoso pero fue: $result")
      val token = result.toOption.get
      assert(token.nonEmpty)
      assert(token.contains("."))
    }
  }

  test("LoginService con contraseña incorrecta devuelve Left con 'Credenciales inválidas'") {
    loginService.login(Credentials("patricia", "contraseña_equivocada")).map { result =>
      assertEquals(result, Left("Credenciales inválidas"))
    }
  }

  test("LoginService con usuario inexistente devuelve Left con 'Credenciales inválidas' (sin revelar que el usuario no existe)") {
    loginService.login(Credentials("usuario_fantasma", "cualquier_pass")).map { result =>
      assertEquals(result, Left("Credenciales inválidas"))
    }
  }

  test("LoginService: el token generado contiene el userId correcto del usuario autenticado") {
    for {
      loginResult <- loginService.login(Credentials("patricia", "mi_password_segura"))
      token = loginResult.toOption.get
      validationResult <- jwtService.validateToken(token)
    } yield {
      assertEquals(validationResult, Right(1L)) // patricia tiene id = 1
    }
  }
}
