using Npgsql;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Domain.Models.Auth;

namespace SphereChat.Api.Infrastructure.Postgres;

/// <summary>
/// Adaptador de persistencia para usuarios del módulo funcional (Chat Auth).
/// Equivalente a SkunkAuthUserRepository.scala
/// </summary>
public class NpgsqlAuthUserRepository : IAuthUserRepository
{
    private readonly string _connectionString;

    public NpgsqlAuthUserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<AuthUser?> FindByUsernameAsync(string username)
    {
        const string sql = @"
            SELECT id, username::text, password_hash
            FROM ""credentials"".""users""
            WHERE username = @username
            LIMIT 1";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("username", username);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new AuthUser(
            Id: reader.GetInt64(0),
            Username: reader.GetString(1),
            PasswordHash: reader.GetString(2)
        );
    }
}
