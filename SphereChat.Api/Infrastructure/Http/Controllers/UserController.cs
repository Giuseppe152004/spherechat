using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SphereChat.Api.Application.Ports.Out;
using Npgsql;
using System.Security.Claims;

namespace SphereChat.Api.Infrastructure.Http.Controllers;

public record UpdateProfileRequest(string DisplayName, string? AvatarUrl, int Status, string? PresenceNote, string? Bio);

[ApiController]
[Route("api/v1/users")]
[Authorize]
[Tags("Usuarios - Perfil")]
public class UserController : ControllerBase
{
    private readonly string _connectionString;
    private readonly IEventPublisher _eventPublisher;

    public UserController(IConfiguration configuration, IEventPublisher eventPublisher)
    {
        _connectionString = configuration.GetConnectionString("SphereDb") ?? configuration["Database:Chat"] ?? "";
        _eventPublisher = eventPublisher;
    }

    private long GetUserId() =>
        long.Parse(User.FindFirst("userId")?.Value
            ?? throw new UnauthorizedAccessException("userId no encontrado en el token"));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = GetUserId();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Asegurar que la columna existe (en caso de que sea la primera vez)
            await EnsureAvatarColumnExistsAsync(conn);

            const string sql = @"
                UPDATE ""credentials"".""users""
                SET avatar_url = @avatarUrl,
                    display_name = @displayName,
                    bio = @bio,
                    status = @status,
                    is_online = @isOnline
                WHERE id = @id";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("avatarUrl", (object?)request.AvatarUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("displayName", (object?)request.DisplayName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("bio", (object?)request.Bio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("status", request.Status);
            cmd.Parameters.AddWithValue("isOnline", request.Status != 3);
            cmd.Parameters.AddWithValue("id", userId);

            await cmd.ExecuteNonQueryAsync();

            string statusStr = request.Status switch {
                1 => "Away",
                2 => "DoNotDisturb",
                3 => "Offline",
                _ => "Online"
            };
            
            var avatarUrlForPayload = string.IsNullOrEmpty(request.AvatarUrl) || request.AvatarUrl == "none"
                ? "none"
                : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.AvatarUrl));

            string presencePayload = $"{userId}:{(request.Status != 3).ToString().ToLower()}:{statusStr}:{avatarUrlForPayload}";

            await _eventPublisher.PublishAsync("sphere_updates", "USER_PRESENCE", presencePayload);

            var notifyJson = System.Text.Json.JsonSerializer.Serialize(new { Type = "USER_PRESENCE", Payload = presencePayload });
            await using var notifyCmd = new NpgsqlCommand("SELECT pg_notify('sphere_updates', @payload)", conn);
            notifyCmd.Parameters.AddWithValue("payload", notifyJson);
            await notifyCmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Perfil actualizado correctamente." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error Interno: {ex.Message}");
        }
    }

    private async Task EnsureAvatarColumnExistsAsync(NpgsqlConnection conn)
    {
        try
        {
            const string sql = @"
                DO $$ 
                BEGIN 
                    BEGIN
                        ALTER TABLE ""credentials"".""users"" ADD COLUMN avatar_url text;
                    EXCEPTION
                        WHEN duplicate_column THEN null;
                    END;
                    BEGIN
                        ALTER TABLE ""credentials"".""users"" ADD COLUMN display_name text;
                    EXCEPTION
                        WHEN duplicate_column THEN null;
                    END;
                    BEGIN
                        ALTER TABLE ""credentials"".""users"" ADD COLUMN bio text;
                    EXCEPTION
                        WHEN duplicate_column THEN null;
                    END;
                END $$;";
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
        catch 
        {
            // Ignorar errores si no tenemos permisos para alterar, intentamos seguir.
        }
    }
}
