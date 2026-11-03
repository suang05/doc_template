using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Preview use-case: Generates PDF stream directly for Monaco/Form preview with zero side-effects.
/// Rule: NEVER upload to MinIO, NEVER write to generation_logs, NEVER increment versions.
/// </summary>
public sealed class PreviewDocumentUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IRenderEngineFactory engineFactory) : IUseCase<PreviewDocumentQuery, Stream>
{
    public async Task<Stream> ExecuteAsync(PreviewDocumentQuery query, CancellationToken ct = default)
    {
        Stream templateStream;
        RenderEngineType engineType;

        // If Monaco editor supplied the active HTML in the body, use it directly!
        if (!string.IsNullOrWhiteSpace(query.Html))
        {
            templateStream = await HybridSpoolStream.CreateFromUtf8StringAsync(query.Html, ct: ct);
            engineType = RenderEngineType.Html;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(query.Slug))
                throw new DomainValidationException("Template slug or HTML content is required for preview.");

            var template = await templateRepo.GetBySlugAsync(query.Slug, ct)
                ?? throw new NotFoundException($"Template '{query.Slug}' not found.");

            var version = template.GetPublishedVersion();

            engineType = version.GetRenderEngineType();

            templateStream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        }

        try
        {
            var engine = engineFactory.GetEngine(engineType);

            string dataJson = query.Payload.ValueKind != JsonValueKind.Undefined ? query.Payload.GetRawText() : "{}";

            // Always render to PDF for preview
            return await engine.RenderStreamAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
        }
        finally
        {
            await templateStream.DisposeAsync();
        }
    }
}
