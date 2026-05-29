using SphereChat.Api.Domain.Models.Auth;

namespace SphereChat.Api.Application.Ports.In;

/// <summary>
/// Servicio JWT funcional para el módulo de Chat + Auth.
/// Equivalente a JwtService.scala
/// </summary>
public interface IJwtService
{
    Task<string> GenerateTokenAsync(long userId);
    Task<long?> ValidateTokenAsync(string token);
}
