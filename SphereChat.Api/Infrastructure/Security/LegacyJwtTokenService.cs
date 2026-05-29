using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Legacy;

namespace SphereChat.Api.Infrastructure.Security;

/// <summary>
/// Servicio JWT legacy con claims de documento (DNI/CE).
/// Equivalente a JwtTokenService.scala
/// </summary>
public class LegacyJwtTokenService : ILegacyTokenService
{
    private readonly SymmetricSecurityKey _key;
    private const int ExpirationHours = 8;

    public LegacyJwtTokenService(string secret)
    {
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public Task<AuthToken> GenerateTokenAsync(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.DocumentNumber.Value),
                new Claim("docType", user.DocumentType.ToString()),
                new Claim("name", $"{user.Nombres} {user.Apellidos}"),
                new Claim("nombres", user.Nombres),
                new Claim("apellidos", user.Apellidos),
                new Claim("observaciones", user.Observaciones ?? "")
            }),
            Expires = DateTime.UtcNow.AddHours(ExpirationHours),
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Task.FromResult(new AuthToken(tokenHandler.WriteToken(token)));
    }

    public Task<(DocumentType DocType, DocumentNumber DocNum)?> ValidateTokenAsync(string token)
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
            var docNum = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var docTypeStr = principal.FindFirst("docType")?.Value;

            if (docNum is null || docTypeStr is null) return Task.FromResult<(DocumentType, DocumentNumber)?>(null);

            var docType = docTypeStr == "DNI" ? DocumentType.DNI : DocumentType.CE;
            return Task.FromResult<(DocumentType, DocumentNumber)?>((docType, new DocumentNumber(docNum)));
        }
        catch
        {
            return Task.FromResult<(DocumentType, DocumentNumber)?>(null);
        }
    }
}
