using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using SphereChat.Api.Application.Ports.In;

namespace SphereChat.Api.Infrastructure.Http.Controllers;

public record UploadResponse(string Url, string FileName, string Extension, long SizeBytes);

/// <summary>
/// Controlador de Upload de archivos multimedia y streaming estático.
/// Equivalente a FileUploadRoutes.scala
///
/// Patrón "Upload &amp; Notify":
///   El cliente sube el archivo aquí, recibe la URL pública,
///   y luego la envía a POST /messages/video, /messages/image, etc.
/// </summary>
[ApiController]
[Tags("Upload - Archivos")]
public class FileUploadController : ControllerBase
{
    private readonly IJwtService _jwtService;
    private readonly string _uploadDir;
    private readonly string _publicBaseUrl;

    // Extensiones permitidas para evitar subir ejecutables maliciosos
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Imágenes
        "jpg", "jpeg", "png", "gif", "webp", "bmp", "svg",
        // Videos
        "mp4", "mov", "avi", "mkv", "webm",
        // Audios
        "mp3", "ogg", "wav", "aac", "m4a", "opus",
        // Documentos
        "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "csv", "zip", "rar"
    };

    public FileUploadController(IJwtService jwtService, IConfiguration config)
    {
        _jwtService = jwtService;
        _uploadDir = config["Upload:Directory"] ?? @"C:\spherechat_uploads";
        _publicBaseUrl = Environment.GetEnvironmentVariable("PUBLIC_API_URL")
            ?? config["Upload:PublicBaseUrl"]
            ?? "http://10.10.40.5:8082";

        // Asegurar que el directorio de uploads existe al iniciar
        Directory.CreateDirectory(_uploadDir);
    }

    /// <summary>
    /// Carga de archivos multipart — Protegido por JWT.
    /// POST /api/v1/chat/upload
    /// </summary>
    [HttpPost("api/v1/chat/upload")]
    [Authorize]
    [RequestSizeLimit(100_000_000)] // 100MB máximo
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No se encontró el campo 'file' en el formulario multipart. Envía el archivo con el nombre 'file'.");

        var originalName = file.FileName;
        var dotIdx = originalName.LastIndexOf('.');
        var extension = dotIdx > 0 ? originalName[(dotIdx + 1)..].ToLowerInvariant() : "bin";
        var baseName = dotIdx > 0 ? originalName[..dotIdx] : originalName;

        if (!AllowedExtensions.Contains(extension))
            return BadRequest($"Extensión '{extension}' no permitida. Extensiones válidas: {string.Join(", ", AllowedExtensions)}");

        // Generar nombre único para evitar colisiones
        var uniqueName = $"{Guid.NewGuid()}_{baseName}.{extension}";
        var targetPath = Path.Combine(_uploadDir, uniqueName);

        // Escribir bytes al disco de forma segura
        await using var stream = new FileStream(targetPath, FileMode.Create);
        await file.CopyToAsync(stream);

        var publicUrl = $"{_publicBaseUrl}/uploads/{uniqueName}";

        return Ok(new UploadResponse(
            Url: publicUrl,
            FileName: baseName,
            Extension: extension,
            SizeBytes: file.Length
        ));
    }

    /// <summary>
    /// Servidor de archivos estáticos: GET /uploads/{fileName}
    /// PÚBLICO — Sin autenticación — Permite streaming directo (Partial Content/Range Requests).
    /// Los reproductores nativos del Frontend (C#/Avalonia) inyectan la URL directamente.
    /// </summary>
    [HttpGet("/uploads/{fileName}")]
    [AllowAnonymous]
    public IActionResult GetStaticFile(string fileName)
    {
        var filePath = Path.Combine(_uploadDir, fileName);

        // Seguridad: Evitar path traversal (../)
        var normalizedUpload = Path.GetFullPath(_uploadDir);
        var normalizedFile = Path.GetFullPath(filePath);
        if (!normalizedFile.StartsWith(normalizedUpload))
            return Forbid("Acceso denegado");

        if (!System.IO.File.Exists(filePath))
            return NotFound($"Archivo '{fileName}' no encontrado");

        // Detectar Content-Type automáticamente
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileName, out var contentType))
            contentType = "application/octet-stream";

        // EnableRangeProcessing = true habilita Partial Content (206) automáticamente
        // Esto es CRÍTICO para streaming de video/audio en el Frontend C#/Avalonia
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }
}
