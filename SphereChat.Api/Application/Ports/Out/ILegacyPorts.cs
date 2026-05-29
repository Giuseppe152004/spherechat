using SphereChat.Api.Domain.Models.Legacy;

namespace SphereChat.Api.Application.Ports.Out;

/// <summary>
/// Puerto de persistencia legacy para usuarios con DNI/CE.
/// Equivalente a UserRepository.scala
/// </summary>
public interface IUserRepository
{
    Task<User?> FindUserAsync(DocumentType docType, DocumentNumber docNum, string requestIp);
}

/// <summary>
/// Puerto para generación/validación de tokens legacy (con claims de documento).
/// Equivalente a TokenService.scala
/// </summary>
public interface ILegacyTokenService
{
    Task<AuthToken> GenerateTokenAsync(User user);
    Task<(DocumentType DocType, DocumentNumber DocNum)?> ValidateTokenAsync(string token);
}

/// <summary>
/// Validador de API Keys.
/// Equivalente a ApiKeyValidator.scala
/// </summary>
public interface IApiKeyValidator
{
    Task<bool> IsValidAsync(string apiKey);
}

/// <summary>
/// Puerto de persistencia para Capacitación.
/// Equivalente a CapacitacionRepository.scala
/// </summary>
public interface ICapacitacionRepository
{
    Task<bool> ExistsPostulanteAsync(int id);
    Task<(DocumentType DocType, DocumentNumber DocNum, string Nombre)?> GetPostulanteDocumentAsync(int id);
    Task RegistrarAsistenciaAsync(int postulanteId, int diaCapacitacion, bool asistio);
    Task<int> CountAsistenciasValidasAsync(int postulanteId);
    Task RegistrarBonoAsync(BonoCapacitacion bono);
    Task<PostulanteResumen?> GetResumenAsync(int postulanteId);
    Task<List<PostulanteBasico>> ListarPostulantesAsync();
    Task<List<PostulanteDetalle>> ListarPostulantesPorUsuarioAsync(int idUser);
}
