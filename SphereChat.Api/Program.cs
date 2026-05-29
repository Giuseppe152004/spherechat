using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using SphereChat.Api.Application.Ports.In;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Application.Services;
using SphereChat.Api.Infrastructure.Http.Hubs;
using SphereChat.Api.Infrastructure.Postgres;
using SphereChat.Api.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════
// 1. Configuración de Connection Strings
// ═══════════════════════════════════════════════════════════════════
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "mi-api-scala-secreto-desarrollo-2026";
var chatDbConnStr = builder.Configuration["Database:Chat"]
    ?? "Host=10.10.40.247;Port=5432;Database=sphere;Username=patricia;Password=123456;";
var legacyDbConnStr = builder.Configuration["Database:Legacy"]
    ?? "Host=142.44.158.217;Port=5432;Database=ecosystem_dev;Username=api_training_user;Password=Pepeluchoelquetequieremucho;Search Path=training_dev;";

// ═══════════════════════════════════════════════════════════════════
// 2. JWT Authentication (compatible con el JwtService de Scala)
// ═══════════════════════════════════════════════════════════════════
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
        
        // Soportar Auth JWT en WebSockets (SignalR) vía query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/call"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ═══════════════════════════════════════════════════════════════════
// 3. Dependency Injection — Arquitectura Hexagonal (Ports & Adapters)
// ═══════════════════════════════════════════════════════════════════

// --- Seguridad ---
builder.Services.AddSingleton<IJwtService>(new JwtService(jwtSecret, 24));
builder.Services.AddSingleton<ILegacyTokenService>(new LegacyJwtTokenService(jwtSecret));
builder.Services.AddSingleton<IApiKeyValidator>(new DummyApiKeyValidator());

// --- Repositorios (Chat DB — sphere) ---
builder.Services.AddSingleton<IMessageRepository>(new NpgsqlMessageRepository(chatDbConnStr));
builder.Services.AddSingleton<IAuthUserRepository>(new NpgsqlAuthUserRepository(chatDbConnStr));

// --- Repositorios (Legacy DB — ecosystem_dev) ---
builder.Services.AddSingleton<IUserRepository>(new NpgsqlUserRepository(legacyDbConnStr));
builder.Services.AddSingleton<ICapacitacionRepository>(new NpgsqlCapacitacionRepository(legacyDbConnStr));

// --- Servicios de Aplicación ---
builder.Services.AddSingleton<ISendMessageUseCase, SendMessageService>();
builder.Services.AddSingleton<LoginService>();
builder.Services.AddSingleton<LegacyAuthService>();
builder.Services.AddSingleton<CapacitacionService>();
builder.Services.AddSingleton<ICallSessionTracker, CallSessionTracker>();

// ═══════════════════════════════════════════════════════════════════
// 4. Controladores + SignalR + OpenAPI + CORS
// ═══════════════════════════════════════════════════════════════════
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ═══════════════════════════════════════════════════════════════════
// 5. Build & Configure Middleware Pipeline
// ═══════════════════════════════════════════════════════════════════
var app = builder.Build();

// CORS (antes de auth)
app.UseCors();

// Auth
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI + Scalar Docs
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "SphereChat API — Auth, Chat & Capacitación";
    options.Theme = ScalarTheme.DeepSpace;
});

// Controladores y SignalR Hubs
app.MapControllers();
app.MapHub<CallHub>("/hubs/call");

// ═══════════════════════════════════════════════════════════════════
// 6. Configurar puerto y arranque
// ═══════════════════════════════════════════════════════════════════
var uploadDir = builder.Configuration["Upload:Directory"] ?? @"C:\spherechat_uploads";
var publicBaseUrl = Environment.GetEnvironmentVariable("PUBLIC_API_URL")
    ?? builder.Configuration["Upload:PublicBaseUrl"]
    ?? "http://10.10.40.5:8082";

Directory.CreateDirectory(uploadDir);

app.Urls.Add("http://0.0.0.0:8082");

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"✅ Servidor ASP.NET Core levantado con éxito en http://0.0.0.0:8082");
    Console.WriteLine($"📜 Documentación Scalar en: http://localhost:8082/scalar/v1");
    Console.WriteLine($"🌐 URL pública de archivos multimedia: {publicBaseUrl}/uploads/");
    Console.WriteLine($"🚀 Módulo de Chat conectado a PostgreSQL ({chatDbConnStr.Split(';')[0]})");
    Console.WriteLine($"📁 Archivos estáticos servidos desde: {Path.GetFullPath(uploadDir)}");
    Console.WriteLine($"📤 Endpoint de Upload: POST {publicBaseUrl}/api/v1/chat/upload");
    Console.WriteLine($"📜 Historial: GET {publicBaseUrl}/api/v1/chat/rooms/{{roomId}}/messages");
});

app.Run();

// ═══════════════════════════════════════════════════════════════════
// Implementación inline del ApiKeyValidator dummy (igual que en Scala)
// ═══════════════════════════════════════════════════════════════════
public class DummyApiKeyValidator : IApiKeyValidator
{
    public Task<bool> IsValidAsync(string apiKey) => Task.FromResult(apiKey == "secret-api-key");
}
