using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Legacy;
using SphereChat.Api.Domain.Errors;

namespace SphereChat.Api.Application.Services;

/// <summary>
/// Servicio de Capacitación.
/// Equivalente a CapacitacionService.scala
/// </summary>
public class CapacitacionService
{
    private readonly ICapacitacionRepository _repository;

    public CapacitacionService(ICapacitacionRepository repository)
    {
        _repository = repository;
    }

    public async Task RegistrarAsistenciaAsync(int postulanteId, int diaCapacitacion, bool asistio)
    {
        var exists = await _repository.ExistsPostulanteAsync(postulanteId);
        if (!exists) throw new PostulanteNotFoundError();

        if (diaCapacitacion < 1 || diaCapacitacion > 7)
            throw new InvalidDiaCapacitacionError();

        await _repository.RegistrarAsistenciaAsync(postulanteId, diaCapacitacion, asistio);
    }

    public async Task<PostulanteResumen> ObtenerResumenAsync(int postulanteId)
    {
        var resumen = await _repository.GetResumenAsync(postulanteId);
        if (resumen is null) throw new PostulanteNotFoundError();
        return resumen;
    }

    public Task<List<PostulanteBasico>> ListarPostulantesAsync()
        => _repository.ListarPostulantesAsync();

    public Task<List<PostulanteDetalle>> ListarMisPostulantesAsync(int idUser)
        => _repository.ListarPostulantesPorUsuarioAsync(idUser);
}
