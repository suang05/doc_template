using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Stateless preview use-case: Generates PDF bytes directly from an uploaded file stream and JSON payload.
/// Rule: NEVER upload to MinIO, NEVER write to generation_logs, NEVER increment versions.
/// </summary>
public sealed class RenderStatelessDocumentUseCase(IEnumerable<IRenderEngine> engines)
{
    public async Task<Stream> ExecuteStreamAsync(Stream fileStream, string fileName, string? jsonData, CancellationToken ct = default)
    {
        var engineType = GetEngineTypeFromFileName(fileName);
        var engine = engines.FirstOrDefault(e => e.EngineType == engineType) 
            ?? throw new NotSupportedException($"No render engine found for {engineType}");

        string payload = string.IsNullOrWhiteSpace(jsonData) ? "{}" : jsonData;

        // Render stateless document to PDF stream
        return await engine.RenderStreamAsync(fileStream, payload, OutputFormat.Pdf, ct);
    }

    public async Task<byte[]> ExecuteAsync(Stream fileStream, string fileName, string? jsonData, CancellationToken ct = default)
    {
        await using var stream = await ExecuteStreamAsync(fileStream, fileName, jsonData, ct);
        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var copyMs = new MemoryStream();
        await stream.CopyToAsync(copyMs, ct);
        return copyMs.ToArray();
    }

    private RenderEngineType GetEngineTypeFromFileName(string fileName)
    {
        string ext = Path.GetExtension(fileName);
        if (TemplateFormat.TryFromExtension(ext, out var format))
        {
            return format.DefaultEngineType;
        }

        throw new NotSupportedException($"Unsupported file extension: {ext}");
    }
}
