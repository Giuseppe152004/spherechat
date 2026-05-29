using Npgsql;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Legacy;
using System.Text.Json;

namespace SphereChat.Api.Infrastructure.Postgres;

/// <summary>
/// Repositorio Legacy de Capacitación.
/// Equivalente a PostgresCapacitacionRepository.scala
/// Mismas queries SQL idénticas apuntando a training_dev.
/// </summary>
public class NpgsqlCapacitacionRepository : ICapacitacionRepository
{
    private readonly string _connectionString;

    public NpgsqlCapacitacionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> ExistsPostulanteAsync(int id)
    {
        const string sql = "SELECT 1 FROM training_dev.postulants WHERE id_postulant = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync();
    }

    public async Task<(DocumentType DocType, DocumentNumber DocNum, string Nombre)?> GetPostulanteDocumentAsync(int id)
    {
        const string sql = @"
            SELECT document_type, document_number, first_name || ' ' || last_name AS nombre
            FROM training_dev.postulants WHERE id_postulant = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var docType = reader.GetString(0) == "DNI" ? DocumentType.DNI : DocumentType.CE;
        return (docType, new DocumentNumber(reader.GetString(1)), reader.GetString(2));
    }

    public async Task RegistrarAsistenciaAsync(int postulanteId, int diaCapacitacion, bool asistio)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // UPSERT: Intenta UPDATE primero, si no afecta filas entonces INSERT
        const string updateSql = @"
            UPDATE training_dev.daily_control
            SET attendance = @asistio, attendance_date = CURRENT_DATE
            WHERE id_postulant = @postulanteId AND training_day = @dia";

        await using var updateCmd = new NpgsqlCommand(updateSql, conn);
        updateCmd.Parameters.AddWithValue("asistio", asistio);
        updateCmd.Parameters.AddWithValue("postulanteId", postulanteId);
        updateCmd.Parameters.AddWithValue("dia", diaCapacitacion);
        var rowsAffected = await updateCmd.ExecuteNonQueryAsync();

        if (rowsAffected == 0)
        {
            const string insertSql = @"
                INSERT INTO training_dev.daily_control (id_postulant, training_day, attendance, attendance_date)
                VALUES (@postulanteId, @dia, @asistio, CURRENT_DATE)";

            await using var insertCmd = new NpgsqlCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("postulanteId", postulanteId);
            insertCmd.Parameters.AddWithValue("dia", diaCapacitacion);
            insertCmd.Parameters.AddWithValue("asistio", asistio);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<int> CountAsistenciasValidasAsync(int postulanteId)
    {
        const string sql = @"
            SELECT COUNT(*)::INT
            FROM training_dev.daily_control
            WHERE id_postulant = @id AND attendance = true";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", postulanteId);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public Task RegistrarBonoAsync(BonoCapacitacion bono) => Task.CompletedTask; // Cálculo dinámico en SELECT

    public async Task<PostulanteResumen?> GetResumenAsync(int postulanteId)
    {
        // La consulta SQL analítica de alta velocidad proporcionada por el DBA (idéntica al Scala)
        const string sql = @"
            SELECT
                p.id_postulant AS postulanteId,
                CONCAT(p.first_name, ' ', p.last_name) AS nombreCompleto,
                p.document_type AS tipoDocumento,
                p.document_number AS numeroDocumento,
                COUNT(CASE WHEN dc.attendance = true THEN 1 END)::INT AS asistenciasRegistradas,
                SUM(CASE WHEN dc.training_day BETWEEN 1 AND 7 AND dc.attendance = true THEN 15.00 ELSE 0.00 END)::DOUBLE PRECISION AS montoBonoAcumulado,
                COALESCE(ps.code, 'EN_CAPACITACION') AS estadoScorecard,
                p.operational_validation AS validacionOperativa,
                CASE
                    WHEN EXTRACT(DAY FROM p.start_date) BETWEEN 1 AND 15 THEN 1
                    ELSE 2
                END::INT AS corteOperativo,
                CASE
                    WHEN EXTRACT(DAY FROM p.start_date) BETWEEN 1 AND 15
                        THEN (p.start_date + INTERVAL '1 month')::DATE - EXTRACT(DAY FROM p.start_date + INTERVAL '1 month')::INT + 18
                    ELSE
                        (p.start_date + INTERVAL '2 month')::DATE - EXTRACT(DAY FROM p.start_date + INTERVAL '2 month')::INT + 5
                END AS fechaPagoEstimada
            FROM training_dev.postulants p
            LEFT JOIN training_dev.postulant_statuses ps ON p.id_state = ps.id_state
            LEFT JOIN training_dev.daily_control dc ON p.id_postulant = dc.id_postulant
            WHERE p.id_postulant = @id
            GROUP BY p.id_postulant, ps.code, p.first_name, p.last_name, p.document_type, p.document_number, p.operational_validation, p.start_date";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", postulanteId);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync()) return null;

        var docType = reader.GetString(2) == "DNI" ? DocumentType.DNI : DocumentType.CE;
        var score = reader.GetString(6);
        var estadoGeneral = score switch
        {
            "ALTA" => PostulanteEstado.ALTA,
            "NO_APTO" => PostulanteEstado.NO_APTO,
            _ => PostulanteEstado.EN_CAPACITACION
        };

        var fechaPago = reader.IsDBNull(9) ? (DateOnly?)null : DateOnly.FromDateTime(reader.GetDateTime(9));

        // Segunda consulta: Historial detallado de asistencias día a día
        var historial = await GetHistorialDetalleAsync(conn, postulanteId);

        return new PostulanteResumen(
            PostulanteId: reader.GetInt32(0),
            NombreCompleto: reader.GetString(1),
            TipoDocumento: docType,
            NumeroDocumento: new DocumentNumber(reader.GetString(3)),
            AsistenciasRegistradas: reader.GetInt32(4),
            MontoBonoAcumulado: reader.GetDouble(5),
            EstadoScorecard: score,
            ValidacionOperativa: reader.IsDBNull(7) ? null : reader.GetBoolean(7),
            CorteOperativo: reader.IsDBNull(8) ? null : reader.GetInt32(8),
            FechaPagoEstimada: fechaPago,
            EstadoGeneral: estadoGeneral,
            HistorialAsistencias: historial
        );
    }

    private static async Task<List<AsistenciaDetalle>> GetHistorialDetalleAsync(NpgsqlConnection conn, int postulanteId)
    {
        const string sql = @"
            SELECT
                dc.training_day AS dia,
                CASE WHEN dc.attendance THEN 'A' ELSE 'F' END AS estado,
                CASE
                    WHEN dc.training_day BETWEEN 1 AND 2 THEN 'Manual'
                    ELSE 'Nexus'
                END AS origen
            FROM training_dev.daily_control dc
            WHERE dc.id_postulant = @id
            ORDER BY dc.training_day";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", postulanteId);
        await using var reader = await cmd.ExecuteReaderAsync();

        var list = new List<AsistenciaDetalle>();
        while (await reader.ReadAsync())
        {
            list.Add(new AsistenciaDetalle(
                Dia: reader.GetInt32(0),
                Estado: reader.GetString(1),
                Origen: reader.GetString(2)
            ));
        }
        return list;
    }

    public async Task<List<PostulanteBasico>> ListarPostulantesAsync()
    {
        const string sql = @"
            SELECT
                p.id_postulant AS id,
                CONCAT(p.first_name, ' ', p.last_name) AS nombre_completo,
                'Sin asignar' AS puesto,
                COALESCE(ps.code, 'EN_CAPACITACION') AS estado
            FROM training_dev.postulants p
            LEFT JOIN training_dev.postulant_statuses ps ON p.id_state = ps.id_state
            ORDER BY p.id_postulant";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var list = new List<PostulanteBasico>();
        while (await reader.ReadAsync())
        {
            var estado = reader.GetString(3) switch
            {
                "ALTA" => PostulanteEstado.ALTA,
                "NO_APTO" => PostulanteEstado.NO_APTO,
                _ => PostulanteEstado.EN_CAPACITACION
            };
            list.Add(new PostulanteBasico(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), estado));
        }
        return list;
    }

    public async Task<List<PostulanteDetalle>> ListarPostulantesPorUsuarioAsync(int idUser)
    {
        const string sql = @"
            SELECT
                p.id_postulant AS postulanteId,
                p.first_name AS nombres,
                p.last_name AS apellidos,
                'Sin asignar' AS puesto,
                COALESCE(ps.code, 'EN_CAPACITACION') AS estado,
                (
                    SELECT COALESCE(json_agg(json_build_object(
                        'dia', sub_dc.training_day,
                        'asistio', sub_dc.attendance
                    ) ORDER BY sub_dc.training_day), '[]'::json)
                    FROM training_dev.daily_control sub_dc
                    WHERE sub_dc.id_postulant = p.id_postulant
                ) AS historialAsistencias
            FROM training_dev.postulants p
            LEFT JOIN training_dev.postulant_statuses ps ON p.id_state = ps.id_state
            WHERE p.id_user = @idUser
            ORDER BY p.last_name ASC";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("idUser", idUser);
        await using var reader = await cmd.ExecuteReaderAsync();

        var list = new List<PostulanteDetalle>();
        while (await reader.ReadAsync())
        {
            var jsonStr = reader.IsDBNull(5) ? "[]" : reader.GetString(5);
            var historial = ParseHistorialJson(jsonStr);

            list.Add(new PostulanteDetalle(
                PostulanteId: reader.GetInt32(0),
                Nombres: reader.GetString(1),
                Apellidos: reader.GetString(2),
                Puesto: reader.GetString(3),
                Estado: reader.GetString(4),
                HistorialAsistencias: historial
            ));
        }
        return list;
    }

    private static List<HistorialAsistencia> ParseHistorialJson(string jsonStr)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var list = new List<HistorialAsistencia>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var dia = element.GetProperty("dia").GetInt32();
                var asistio = element.GetProperty("asistio").GetBoolean();
                list.Add(new HistorialAsistencia(dia, asistio));
            }
            return list;
        }
        catch
        {
            return new List<HistorialAsistencia>();
        }
    }
}
