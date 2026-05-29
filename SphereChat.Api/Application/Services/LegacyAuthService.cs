using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Legacy;

namespace SphereChat.Api.Application.Services;

/// <summary>
/// Servicio de Login Legacy (DNI/CE + IP tracking).
/// Equivalente a AuthService.scala (application.service)
/// </summary>
public class LegacyAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ILegacyTokenService _tokenService;

    public LegacyAuthService(IUserRepository userRepository, ILegacyTokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public async Task<AuthToken?> LoginAsync(UserCredentials credentials)
    {
        var user = await _userRepository.FindUserAsync(
            credentials.DocumentType, credentials.DocumentNumber, credentials.RequestIp);

        if (user is null) return null;
        return await _tokenService.GenerateTokenAsync(user);
    }
}
