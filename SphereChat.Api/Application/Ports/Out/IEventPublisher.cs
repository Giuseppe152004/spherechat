namespace SphereChat.Api.Application.Ports.Out;

/// <summary>
/// Puerto de salida para publicar eventos en tiempo real.
/// Desacopla la lógica de dominio del mecanismo de transporte (Redis Pub/Sub, Postgres NOTIFY, etc.)
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publica un evento tipado en un canal.
    /// </summary>
    /// <param name="channel">Nombre del canal (ej. "sphere_updates")</param>
    /// <param name="type">Tipo de evento (ej. "USER_PRESENCE", "NEW_MESSAGE")</param>
    /// <param name="payload">Payload serializado del evento</param>
    Task PublishAsync(string channel, string type, string payload);
}
