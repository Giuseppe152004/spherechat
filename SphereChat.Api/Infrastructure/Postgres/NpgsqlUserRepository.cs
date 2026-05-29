using Npgsql;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Legacy;

namespace SphereChat.Api.Infrastructure.Postgres;

/// <summary>
/// Repositorio Legacy para usuarios por DNI/CE (con IP tracking).
/// Equivalente a PostgresUserRepository.scala
/// </summary>
public class NpgsqlUserRepository : IUserRepository
{
    private readonly string _connectionString;

    public NpgsqlUserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<User?> FindUserAsync(DocumentType docType, DocumentNumber docNum, string requestIp)
    {
        const string sql = @"
            UPDATE training_dev.postulants
            SET last_log = 'IP: ' || @ip || ' | UA: Desktop App | Fecha: ' || NOW()
            WHERE document_type = @docType
              AND document_number = @docNum
            RETURNING id_postulant, first_name, last_name, remarks";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("ip", requestIp);
        cmd.Parameters.AddWithValue("docType", docType.ToString());
        cmd.Parameters.AddWithValue("docNum", docNum.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new User(
            docType,
            docNum,
            Nombres: reader.GetString(1),
            Apellidos: reader.GetString(2),
            Observaciones: reader.IsDBNull(3) ? null : reader.GetString(3)
        );
    }
}
