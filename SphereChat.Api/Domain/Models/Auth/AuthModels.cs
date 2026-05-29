namespace SphereChat.Api.Domain.Models.Auth;

/// <summary>
/// Credenciales para el Login Funcional (username + password BCrypt).
/// Equivalente a Credentials de Scala.
/// </summary>
public record Credentials(string Username, string PasswordRaw);

/// <summary>
/// Usuario autenticado con hash BCrypt.
/// Equivalente a AuthUser de Scala.
/// </summary>
public record AuthUser(long Id, string Username, string PasswordHash);
