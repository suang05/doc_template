using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Stateless preview use-case: Generates PDF stream directly from an uploaded file stream and JSON payload.
/// Rule: NEVER upload to MinIO, NEVER write to generation_logs, NEVER increment versions.
/// </summary>
public sealed class RenderStatelessDocumentUseCase(
    IRenderEngineFactory engineFactory,
    IDocxSecurityScanner securityScanner) : IUseCase<RenderStatelessDocumentCommand, Stream>
{
    public async Task<Stream> ExecuteAsync(RenderStatelessDocumentCommand command, CancellationToken ct = default)
    {
        string ext = Path.GetExtension(command.FileName).ToLowerInvariant();
        var engineType = GetEngineTypeFromExtension(ext);
        var engine = engineFactory.GetEngine(engineType);

        string payload = string.IsNullOrWhiteSpace(command.JsonData) ? "{}" : command.JsonData;

        if (ext == ".docx")
        {
            await using var docxCopy = await HybridSpoolStream.SpoolFromAsync(command.FileStream, ct: ct);

            var scanResult = securityScanner.Scan(docxCopy);
            if (scanResult is { IsSafe: false })
            {
                throw new DomainValidationException(
                    $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
            }

            docxCopy.Position = 0;
            return await engine.RenderStreamAsync(docxCopy, payload, OutputFormat.Pdf, ct);
        }

        return await engine.RenderStreamAsync(command.FileStream, payload, OutputFormat.Pdf, ct);
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
