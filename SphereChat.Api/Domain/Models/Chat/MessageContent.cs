namespace SphereChat.Api.Domain.Models.Chat;

/// <summary>
/// ADT sellado para los distintos contenidos de un mensaje.
/// Equivalente al sealed trait MessageContent de Scala.
/// </summary>
public abstract record MessageContent;

public sealed record TextContent(string Text) : MessageContent;

public sealed record PhotoContent(
    string Url,
    long? SizeBytes = null,
    string? Caption = null
) : MessageContent;

public sealed record VideoContent(
    string Url,
    double DurationSeconds,
    long? SizeBytes = null,
    string? Thumb = null
) : MessageContent;

public sealed record AudioContent(
    string Url,
    double DurationSeconds,
    string? Waveform = null
) : MessageContent;

public sealed record StickerContent(string StickerId, bool IsAnimated = false) : MessageContent;

public sealed record DocumentContent(
    string Url,
    string FileName,
    string FileExtension,
    long SizeBytes
) : MessageContent;
