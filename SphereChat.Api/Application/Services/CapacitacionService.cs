using Microsoft.Extensions.Caching.Distributed;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Legacy;
using SphereChat.Api.Domain.Errors;
using System.Text.Json;

namespace SphereChat.Api.Application.Services;

/// <summary>
/// Servicio de Capacitación.
/// Implementa caché distribuida en Redis con limpieza automática y expiración.
/// </summary>
public class CapacitacionService
{
    private readonly ICapacitacionRepository _repository;
    private readonly IDistributedCache _cache;

    public CapacitacionService(ICapacitacionRepository repository, IDistributedCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task RegistrarAsistenciaAsync(int postulanteId, int diaCapacitacion, bool asistio)
    {
        var exists = await _repository.ExistsPostulanteAsync(postulanteId);
        if (!exists) throw new PostulanteNotFoundError();

        if (diaCapacitacion < 1 || diaCapacitacion > 7)
            throw new InvalidDiaCapacitacionError();

        await _repository.RegistrarAsistenciaAsync(postulanteId, diaCapacitacion, asistio);
        
        // Limpiar la caché en Redis para que el próximo resumen sea datos frescos
        await _cache.RemoveAsync($"postulante_resumen_{postulanteId}");
    }

    public async Task<PostulanteResumen> ObtenerResumenAsync(int postulanteId)
    {
        var cacheKey = $"postulante_resumen_{postulanteId}";
        
        // 1. Intentar obtener de Redis Cache
        var cachedData = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var cachedResumen = JsonSerializer.Deserialize<PostulanteResumen>(cachedData);
            if (cachedResumen != null) return cachedResumen;
        }

        // 2. Si no está en caché, calcular (Query pesado a la base de datos)
        var resumen = await _repository.GetResumenAsync(postulanteId);
        if (resumen is null) throw new PostulanteNotFoundError();

        // 3. Guardar en Redis con "limpieza automática" (Expira en 10 minutos)
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };
        
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(resumen), cacheOptions);

        return resumen;
    }

    public Task<List<PostulanteBasico>> ListarPostulantesAsync()
        => _repository.ListarPostulantesAsync();

    public Task<List<PostulanteDetalle>> ListarMisPostulantesAsync(int idUser)
        => _repository.ListarPostulantesPorUsuarioAsync(idUser);
}
