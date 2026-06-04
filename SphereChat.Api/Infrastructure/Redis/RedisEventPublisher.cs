using System.Text.Json;
using StackExchange.Redis;
using SphereChat.Api.Application.Ports.Out;

namespace SphereChat.Api.Infrastructure.Redis;

/// <summary>
/// Adaptador de infraestructura que publica eventos vía Redis Pub/Sub.
/// Thread-safe: reutiliza el IConnectionMultiplexer singleton.
/// </summary>
public class RedisEventPublisher : IEventPublisher
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisEventPublisher> _logger;

    public RedisEventPublisher(IConnectionMultiplexer redis, ILogger<RedisEventPublisher> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task PublishAsync(string channel, string type, string payload)
    {
        try
        {
            var subscriber = _redis.GetSubscriber();
            var json = JsonSerializer.Serialize(new { Type = type, Payload = payload });
            var receivers = await subscriber.PublishAsync(RedisChannel.Literal(channel), json);
            _logger.LogDebug("📡 Redis Pub/Sub: Evento '{Type}' publicado en '{Channel}' ({Receivers} suscriptores)", type, channel, receivers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error publicando evento '{Type}' en Redis canal '{Channel}'", type, channel);
        }
    }
}
