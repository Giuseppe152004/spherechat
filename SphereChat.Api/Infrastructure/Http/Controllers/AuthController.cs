using Microsoft.AspNetCore.Mvc;
using SphereChat.Api.Application.Services;
using SphereChat.Api.Domain.Models.Auth;

namespace SphereChat.Api.Infrastructure.Http.Controllers;

/// <summary>
/// Controlador de Auth Funcional (Login con username + BCrypt + JWT).
/// Equivalente a auth.infrastructure.adapter.in.http.AuthEndpoints.scala
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Tags("Auth Funcional")]
public class AuthController : ControllerBase
{
    private readonly LoginService _loginService;

    public AuthController(LoginService loginService)
    {
        _loginService = loginService;
    }

    public record LoginRequest(string Username, string PasswordRaw);
    public record LoginResponse(string Token);

    /// <summary>
    /// Inicia sesión, verifica contraseña Bcrypt y obtiene un JWT.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(typeof(string), 400)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var token = await _loginService.LoginAsync(
            new Credentials(request.Username, request.PasswordRaw));

        if (token is null)
            return BadRequest("Credenciales inválidas");

        return Ok(new LoginResponse(token));
    }
}
