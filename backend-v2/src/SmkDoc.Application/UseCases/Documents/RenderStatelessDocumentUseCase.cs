using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.Engines;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.UseCases.Documents;

/// <summary>
/// Stateless preview use-case: Generates PDF bytes directly from an uploaded file stream and JSON payload.
/// Rule: NEVER upload to MinIO, NEVER write to generation_logs, NEVER increment versions.
/// </summary>
public class RenderStatelessDocumentUseCase
{
    private readonly IEnumerable<IRenderEngine> _engines;

    public RenderStatelessDocumentUseCase(IEnumerable<IRenderEngine> engines)
    {
        _engines = engines;
    }

    public async Task<byte[]> ExecuteAsync(Stream fileStream, string fileName, string? jsonData, CancellationToken ct = default)
    {
        var engineType = GetEngineTypeFromFileName(fileName);
        var engine = _engines.FirstOrDefault(e => e.EngineType == engineType) 
            ?? throw new NotSupportedException($"No render engine found for {engineType}");

        string payload = string.IsNullOrWhiteSpace(jsonData) ? "{}" : jsonData;

        // Render stateless document to PDF
        return await engine.RenderAsync(fileStream, payload, OutputFormat.Pdf, ct);
    }

    private RenderEngineType GetEngineTypeFromFileName(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".docx" => RenderEngineType.Docx,
            ".xlsx" => RenderEngineType.Excel,
            ".html" => RenderEngineType.Html,
            _ => throw new NotSupportedException($"Unsupported file extension: {ext}")
        };
    }
}
