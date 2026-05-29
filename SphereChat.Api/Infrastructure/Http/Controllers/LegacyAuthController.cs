using Microsoft.AspNetCore.Mvc;
using SphereChat.Api.Application.Services;
using SphereChat.Api.Domain.Models.Legacy;

namespace SphereChat.Api.Infrastructure.Http.Controllers;

/// <summary>
/// Controlador Legacy de Auth (Login por DNI/CE + IP tracking).
/// Equivalente a infrastructure.adapter.in.http.auth.AuthEndpoints.scala
/// </summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth Legacy (DNI/CE)")]
public class LegacyAuthController : ControllerBase
{
    private readonly LegacyAuthService _authService;

    public LegacyAuthController(LegacyAuthService authService)
    {
        _authService = authService;
    }

    public record LoginRequest(string DocumentType, string DocumentNumber, string RequestIp);
    public record LoginResponse(string Token);

    /// <summary>
    /// Inicia sesión verificando si DNI/CE existe y obtiene Token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(typeof(object), 404)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var docType = req.DocumentType == "DNI" ? Domain.Models.Legacy.DocumentType.DNI : Domain.Models.Legacy.DocumentType.CE;
        var credentials = new UserCredentials(docType, new DocumentNumber(req.DocumentNumber), req.RequestIp);

        var token = await _authService.LoginAsync(credentials);
        if (token is null)
            return NotFound(new { Code = "USER_NOT_FOUND", Message = "Usuario no encontrado" });

        return Ok(new LoginResponse(token.Value));
    }

    /// <summary>
    /// Prueba de endpoint protegido.
    /// </summary>
    [HttpGet("/api/hello")]
    [ProducesResponseType(typeof(string), 200)]
    public IActionResult ProtectedHello()
    {
        // Este endpoint acepta auth híbrida (API Key o Bearer Token)
        // La validación se hace en el middleware
        return Ok("¡Hola! Tienes acceso al sistema porque tu autenticación fue exitosa.");
    }
}
