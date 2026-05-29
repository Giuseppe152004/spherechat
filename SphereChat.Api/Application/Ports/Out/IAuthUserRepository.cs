using SphereChat.Api.Domain.Models.Auth;

namespace SphereChat.Api.Application.Ports.Out;

/// <summary>
/// Contrato de persistencia para usuarios autenticados (módulo funcional Skunk).
/// Equivalente a AuthUserRepository.scala
/// </summary>
public interface IAuthUserRepository
{
    Task<AuthUser?> FindByUsernameAsync(string username);
}
