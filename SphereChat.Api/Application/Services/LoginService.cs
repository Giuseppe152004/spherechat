using SphereChat.Api.Application.Ports.In;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Auth;

namespace SphereChat.Api.Application.Services;

/// <summary>
/// Servicio de Login funcional con BCrypt.
/// Equivalente a LoginService.scala (auth.application.service)
/// </summary>
public class LoginService
{
    private readonly IAuthUserRepository _userRepo;
    private readonly IJwtService _jwtService;

    public LoginService(IAuthUserRepository userRepo, IJwtService jwtService)
    {
        _userRepo = userRepo;
        _jwtService = jwtService;
    }

    public async Task<string?> LoginAsync(Credentials credentials)
    {
        var user = await _userRepo.FindByUsernameAsync(credentials.Username);
        if (user is null) return null;

        var isValid = BCrypt.Net.BCrypt.Verify(credentials.PasswordRaw, user.PasswordHash);
        if (!isValid) return null;

        return await _jwtService.GenerateTokenAsync(user.Id);
    }
}
