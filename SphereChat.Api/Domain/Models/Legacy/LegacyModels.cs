namespace SphereChat.Api.Domain.Models.Legacy;

// ═══════════════════════════════════════════════════════════════════
// Modelos Legacy de Autenticación (DNI/CE con IP tracking)
// Equivalente a AuthModels.scala
// ═══════════════════════════════════════════════════════════════════

public enum DocumentType { DNI, CE }

public record DocumentNumber(string Value);

public record UserCredentials(
    DocumentType DocumentType,
    DocumentNumber DocumentNumber,
    string RequestIp
);

public record AuthToken(string Value);

public record User(
    DocumentType DocumentType,
    DocumentNumber DocumentNumber,
    string Nombres,
    string Apellidos,
    string? Observaciones
);

// ═══════════════════════════════════════════════════════════════════
// Modelos Legacy de Capacitación
// Equivalente a CapacitacionModels.scala
// ═══════════════════════════════════════════════════════════════════

public enum AsistenciaCodigo { A, F }

public enum PostulanteEstado { EN_CAPACITACION, ALTA, NO_APTO }

public record BonoCapacitacion(
    int PostulanteId,
    int DiasAsistidos,
    double MontoAcumulado,
    int CorteOperativo,
    DateOnly FechaPagoEstimada
);

public record PostulanteBasico(
    int Id,
    string NombreCompleto,
    string Puesto,
    PostulanteEstado Estado
);

public record AsistenciaDetalle(
    int Dia,
    string Estado,
    string Origen
);

public record PostulanteResumen(
    int PostulanteId,
    string NombreCompleto,
    DocumentType TipoDocumento,
    DocumentNumber NumeroDocumento,
    int AsistenciasRegistradas,
    double MontoBonoAcumulado,
    string? EstadoScorecard,
    bool? ValidacionOperativa,
    int? CorteOperativo,
    DateOnly? FechaPagoEstimada,
    PostulanteEstado EstadoGeneral,
    List<AsistenciaDetalle> HistorialAsistencias
);

public record HistorialAsistencia(int Dia, bool Asistio);

public record PostulanteDetalle(
    int PostulanteId,
    string Nombres,
    string Apellidos,
    string Puesto,
    string Estado,
    List<HistorialAsistencia> HistorialAsistencias
);
