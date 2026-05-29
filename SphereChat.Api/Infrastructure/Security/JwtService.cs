using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SphereChat.Api.Application.Ports.In;

namespace SphereChat.Api.Infrastructure.Security;

/// <summary>
/// Servicio JWT funcional para el módulo de Chat + Auth.
/// Equivalente a JwtService.scala (auth.infrastructure.security)
/// </summary>
public class JwtService : IJwtService
{
    private readonly string _secret;
    private readonly SymmetricSecurityKey _key;
    private readonly int _expirationHours;

    public JwtService(string secret, int expirationHours = 24)
    {
        _secret = secret;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _expirationHours = expirationHours;
    }

    public Task<string> GenerateTokenAsync(long userId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("userId", userId.ToString())
            }),
            Expires = DateTime.UtcNow.AddHours(_expirationHours),
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Task.FromResult(tokenHandler.WriteToken(token));
    }

    public Task<long?> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParams, out _);
            var userIdClaim = principal.FindFirst("userId")?.Value;
            if (userIdClaim is null || !long.TryParse(userIdClaim, out var userId))
                return Task.FromResult<long?>(null);

            return Task.FromResult<long?>(userId);
        }
        catch
        {
            return Task.FromResult<long?>(null);
        }
    }
}
