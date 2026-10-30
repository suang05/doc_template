using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Stateless preview use-case: Generates PDF stream directly from an uploaded file stream and JSON payload.
/// Rule: NEVER upload to MinIO, NEVER write to generation_logs, NEVER increment versions.
/// </summary>
public sealed class RenderStatelessDocumentUseCase(
    IEnumerable<IRenderEngine> engines,
    IDocxSecurityScanner securityScanner) : IUseCase<RenderStatelessDocumentCommand, Stream>
{
    public async Task<Stream> ExecuteAsync(RenderStatelessDocumentCommand request, CancellationToken ct = default)
    {
        string ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        var engineType = GetEngineTypeFromExtension(ext);
        var engine = engines.FirstOrDefault(e => e.EngineType == engineType)
            ?? throw new NotSupportedException($"No render engine found for {engineType}");

        string payload = string.IsNullOrWhiteSpace(request.JsonData) ? "{}" : request.JsonData;

        if (ext == ".docx")
        {
            using var docxCopy = new MemoryStream();
            await request.FileStream.CopyToAsync(docxCopy, ct);
            docxCopy.Position = 0;

            var scanResult = securityScanner.Scan(docxCopy);
            if (scanResult is { IsSafe: false })
            {
                throw new DomainValidationException(
                    $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
            }

            docxCopy.Position = 0;
            return await engine.RenderStreamAsync(docxCopy, payload, OutputFormat.Pdf, ct);
        }

        return await engine.RenderStreamAsync(request.FileStream, payload, OutputFormat.Pdf, ct);
    }

    private static RenderEngineType GetEngineTypeFromExtension(string ext)
    {
        if (TemplateFormat.TryFromExtension(ext, out var format))
        {
            return format.DefaultEngineType;
        }

        throw new NotSupportedException($"Unsupported file extension: {ext}");
    }
}
