using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using SphereChat.Api.Application.Services;
using SphereChat.Api.Domain.Errors;
using SphereChat.Api.Domain.Models.Legacy;

namespace SphereChat.Api.Infrastructure.Http.Controllers;

// DTOs de request/response para Capacitación
public record AsistenciaRequest(int PostulanteId, int DiaCapacitacion, string CodigoAsistencia);
public record DocumentDto(string Tipo, string Numero);
public record AsistenciaDetalleResponse(int Dia, string Estado, string Origen);
public record ResumenResponse(
    int PostulanteId, string NombreCompleto, DocumentDto Documento,
    int AsistenciasRegistradas, double MontoBonoAcumulado,
    string? EstadoScorecard, bool? ValidacionOperativa, int? CorteOperativo,
    string? FechaPagoEstimada, string EstadoGeneral,
    List<AsistenciaDetalleResponse> HistorialAsistencias);
public record PostulanteBasicoResponse(
    [property: JsonPropertyName("Id")] int Id, 
    [property: JsonPropertyName("Nombre")] string Nombre, 
    [property: JsonPropertyName("Puesto")] string Puesto, 
    [property: JsonPropertyName("Estado")] string Estado
);

public record HistorialAsistenciaResponse(int Dia, bool Asistio);

public record PostulanteDetalleResponse(
    [property: JsonPropertyName("Id")] int Id, 
    [property: JsonPropertyName("Nombre")] string Nombre, 
    [property: JsonPropertyName("Apellido")] string Apellido, 
    [property: JsonPropertyName("Puesto")] string Puesto, 
    [property: JsonPropertyName("Estado")] string Estado,
    List<HistorialAsistenciaResponse> HistorialAsistencias
);

/// <summary>
/// Controlador de Capacitación.
/// Equivalente a CapacitacionEndpoints.scala
/// </summary>
[ApiController]
[Route("api/v1/capacitacion")]
[Authorize]
[Tags("Capacitación")]
public class CapacitacionController : ControllerBase
{
    private readonly CapacitacionService _service;

    public CapacitacionController(CapacitacionService service)
    {
        _service = service;
    }

    /// <summary>Registra o actualiza asistencia de un postulante (UPSERT)</summary>
    [HttpPost("asistencia")]
    public async Task<IActionResult> RegistrarAsistencia([FromBody] AsistenciaRequest req)
    {
        try
        {
            var asistio = req.CodigoAsistencia == "A";
            await _service.RegistrarAsistenciaAsync(req.PostulanteId, req.DiaCapacitacion, asistio);
            return Ok();
        }
        catch (PostulanteNotFoundError)
        {
            return NotFound(new { Code = "NOT_FOUND", Message = "Postulante no encontrado" });
        }
        catch (InvalidDiaCapacitacionError e)
        {
            return BadRequest(new { Code = "BAD_REQUEST", Message = e.Message });
        }
    }

    /// <summary>Obtiene el resumen consolidado de la capacitación del postulante</summary>
    [HttpGet("postulantes/{id}/resumen")]
    public async Task<IActionResult> ObtenerResumen(string id)
    {
        if (!int.TryParse(id, out var postulanteId))
            return BadRequest(new { Code = "BAD_REQUEST", Message = "ID de postulante inválido" });

        try
        {
            var r = await _service.ObtenerResumenAsync(postulanteId);
            var response = new ResumenResponse(
                r.PostulanteId, r.NombreCompleto,
                new DocumentDto(r.TipoDocumento.ToString(), r.NumeroDocumento.Value),
                r.AsistenciasRegistradas, r.MontoBonoAcumulado,
                r.EstadoScorecard, r.ValidacionOperativa, r.CorteOperativo,
                r.FechaPagoEstimada?.ToString("yyyy-MM-dd"),
                r.EstadoGeneral.ToString(),
                r.HistorialAsistencias.Select(a =>
                    new AsistenciaDetalleResponse(a.Dia, a.Estado, a.Origen)).ToList()
            );
            return Ok(response);
        }
        catch (PostulanteNotFoundError)
        {
            return NotFound(new { Code = "NOT_FOUND", Message = "Postulante no encontrado" });
        }
    }

    /// <summary>Lista todos los postulantes con información básica para la pantalla principal</summary>
    [HttpGet("postulantes")]
    public async Task<IActionResult> ListarPostulantes()
    {
        var lista = await _service.ListarPostulantesAsync();
        var response = lista.Select(p => new PostulanteBasicoResponse(
            p.Id, p.NombreCompleto, p.Puesto, p.Estado.ToString())).ToList();
        return Ok(response);
    }

    /// <summary>Lista los postulantes asignados al capacitador con historial de asistencias</summary>
    [HttpGet("mis-postulantes")]
    public async Task<IActionResult> MisPostulantes([FromQuery] int idUser)
    {
        var lista = await _service.ListarMisPostulantesAsync(idUser);
        var response = lista.Select(p => new PostulanteDetalleResponse(
            p.PostulanteId, p.Nombres, p.Apellidos, p.Puesto, p.Estado,
            p.HistorialAsistencias.Select(h =>
                new HistorialAsistenciaResponse(h.Dia, h.Asistio)).ToList()
        )).ToList();
        return Ok(response);
    }
}
