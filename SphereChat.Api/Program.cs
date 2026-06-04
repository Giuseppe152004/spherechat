using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using StackExchange.Redis;
using SphereChat.Api.Application.Ports.In;
using SphereChat.Api.Application.Ports.Out;
using SphereChat.Api.Application.Services;
using SphereChat.Api.Infrastructure.Http.Hubs;
using SphereChat.Api.Infrastructure.Postgres;
using SphereChat.Api.Infrastructure.Redis;
using SphereChat.Api.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════
// 1. Configuración — SIN fallbacks hardcodeados (Auditoría §3)
// ═══════════════════════════════════════════════════════════════════
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException(
        "FATAL: 'Jwt:Secret' no está configurado en appsettings.json. " +
        "La API no puede arrancar sin un secreto JWT seguro.");

var chatDbConnStr = builder.Configuration["Database:Chat"]
    ?? throw new InvalidOperationException(
        "FATAL: 'Database:Chat' no está configurado en appsettings.json.");

var legacyDbConnStr = builder.Configuration["Database:Legacy"]
    ?? throw new InvalidOperationException(
        "FATAL: 'Database:Legacy' no está configurado en appsettings.json.");

var redisConnStr = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "FATAL: 'ConnectionStrings:Redis' no está configurado en appsettings.json.");

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
// 3. Redis — Caché Distribuida + Pub/Sub + Backplane SignalR
//    (Auditoría §2 + §5)
// ═══════════════════════════════════════════════════════════════════

// 3a. Caché distribuida Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnStr;
    options.InstanceName = "SphereChat:";
});

// 3b. ConnectionMultiplexer compartido (thread-safe, singleton)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse(redisConnStr);
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});

// 3c. Pub/Sub — Puerto de publicación de eventos
builder.Services.AddSingleton<IEventPublisher, RedisEventPublisher>();

// ═══════════════════════════════════════════════════════════════════
// 4. Dependency Injection — Arquitectura Hexagonal (Ports & Adapters)
//    Auditoría §1: Singleton → Scoped para repos con conexiones DB
// ═══════════════════════════════════════════════════════════════════

// --- Seguridad (Singleton OK — sin estado mutable, sin DB) ---
builder.Services.AddSingleton<IJwtService>(new JwtService(jwtSecret, 24));
builder.Services.AddSingleton<ILegacyTokenService>(new LegacyJwtTokenService(jwtSecret));
builder.Services.AddSingleton<IApiKeyValidator>(new DummyApiKeyValidator());

// --- Repositorios (SCOPED — cada request abre/cierra su propia conexión) ---
builder.Services.AddScoped<IMessageRepository>(_ => new NpgsqlMessageRepository(chatDbConnStr));
builder.Services.AddScoped<IAuthUserRepository>(_ => new NpgsqlAuthUserRepository(chatDbConnStr));
builder.Services.AddScoped<IUserRepository>(_ => new NpgsqlUserRepository(legacyDbConnStr));
builder.Services.AddScoped<ICapacitacionRepository>(_ => new NpgsqlCapacitacionRepository(legacyDbConnStr));

// --- Servicios de Aplicación (SCOPED — dependen de repos Scoped) ---
builder.Services.AddScoped<ISendMessageUseCase, SendMessageService>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<LegacyAuthService>();
builder.Services.AddScoped<CapacitacionService>();

// --- Tracking de llamadas (SINGLETON — ConcurrentDictionary thread-safe) ---
builder.Services.AddSingleton<ICallSessionTracker, CallSessionTracker>();

// ═══════════════════════════════════════════════════════════════════
// 5. Controladores + SignalR (con Redis Backplane) + OpenAPI + CORS
// ═══════════════════════════════════════════════════════════════════
builder.Services.AddControllers();

// SignalR con Redis Backplane para escalabilidad multi-instancia
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnStr, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("SphereChat");
    });

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
// 6. Build & Configure Middleware Pipeline
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
// 7. Configurar puerto y arranque
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
    Console.WriteLine($"🔴 Redis conectado a: {redisConnStr}");
    Console.WriteLine($"🔁 SignalR Backplane: Redis ({redisConnStr})");
});

app.Run();

// ═══════════════════════════════════════════════════════════════════
// Implementación inline del ApiKeyValidator dummy (igual que en Scala)
// ═══════════════════════════════════════════════════════════════════
public class DummyApiKeyValidator : IApiKeyValidator
{
    public Task<bool> IsValidAsync(string apiKey) => Task.FromResult(apiKey == "secret-api-key");
}
