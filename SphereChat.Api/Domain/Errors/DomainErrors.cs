namespace SphereChat.Api.Domain.Errors;

// ═══════════════════════════════════════════════════════════════════
// Errores de Autenticación
// Equivalente a AuthError.scala
// ═══════════════════════════════════════════════════════════════════

public abstract class AuthError : Exception
{
    protected AuthError(string message) : base(message) { }
}

public class InvalidCredentialsError() : AuthError("Credenciales inválidas");
public class UserNotFoundError() : AuthError("Usuario no encontrado");
public class InvalidTokenError() : AuthError("Token inválido o expirado");
public class InvalidApiKeyError() : AuthError("API Key inválida");
public class AccessDeniedError(string msg = "Acceso denegado") : AuthError(msg);

// ═══════════════════════════════════════════════════════════════════
// Errores de Capacitación
// Equivalente a CapacitacionError.scala
// ═══════════════════════════════════════════════════════════════════

public abstract class CapacitacionError : Exception
{
    protected CapacitacionError(string message) : base(message) { }
}

public class PostulanteNotFoundError() : CapacitacionError("Postulante no encontrado");
public class InvalidDiaCapacitacionError() : CapacitacionError("El día de capacitación debe ser entre 1 y 7");
public class DatabaseError(string msg) : CapacitacionError(msg);
