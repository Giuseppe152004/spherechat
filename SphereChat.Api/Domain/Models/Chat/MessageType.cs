namespace SphereChat.Api.Domain.Models.Chat;

/// <summary>
/// Discriminador de tipos de mensaje mapeado a la columna "type" (int2) en PostgreSQL.
/// Valores idénticos al enum Scala original.
/// </summary>
public enum MessageType : short
{
    Text = 1,
    Photo = 2,
    Video = 3,
    Audio = 4,
    Sticker = 5,
    Document = 6
}
