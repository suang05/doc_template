using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Stateless preview use-case: Generates PDF stream directly from an uploaded file stream and JSON payload.
/// Rule: NEVER upload to MinIO, NEVER write to generation_logs, NEVER increment versions.
/// </summary>
public sealed class RenderStatelessDocumentUseCase(IEnumerable<IRenderEngine> engines)
    : IUseCase<RenderStatelessDocumentCommand, Stream>
{
    public async Task<Stream> ExecuteAsync(RenderStatelessDocumentCommand request, CancellationToken ct = default)
    {
        var engineType = GetEngineTypeFromFileName(request.FileName);
        var engine = engines.FirstOrDefault(e => e.EngineType == engineType) 
            ?? throw new NotSupportedException($"No render engine found for {engineType}");

        string payload = string.IsNullOrWhiteSpace(request.JsonData) ? "{}" : request.JsonData;

        // Render stateless document to PDF stream
        return await engine.RenderStreamAsync(request.FileStream, payload, OutputFormat.Pdf, ct);
    }

    private static RenderEngineType GetEngineTypeFromFileName(string fileName)
    {
        string ext = Path.GetExtension(fileName);
        if (TemplateFormat.TryFromExtension(ext, out var format))
        {
            return format.DefaultEngineType;
        }

        throw new NotSupportedException($"Unsupported file extension: {ext}");
    }
}
