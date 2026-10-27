using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Domain.Entities;
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
    IEnumerable<IRenderEngine> engines) : IUseCase<PreviewDocumentQuery, Stream>
{
    public async Task<Stream> ExecuteAsync(PreviewDocumentQuery request, CancellationToken ct = default)
    {
        Stream templateStream;
        RenderEngineType engineType;

        // If Monaco editor supplied the active HTML in the body, use it directly!
        if (!string.IsNullOrWhiteSpace(request.Html))
        {
            templateStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(request.Html));
            engineType = RenderEngineType.Html;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Slug))
                throw new DomainValidationException("Template slug or HTML content is required for preview.");

            var template = await templateRepo.GetBySlugAsync(request.Slug, ct)
                ?? throw new NotFoundException($"Template '{request.Slug}' not found.");

            if (template.CurrentVersionId is null)
                throw new BusinessRuleViolationException($"Template '{request.Slug}' has no published version.");

            var version = template.CurrentVersion
                ?? await templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct)
                ?? throw new NotFoundException($"Current version for template '{request.Slug}' not found.");

            engineType = version.GetRenderEngineType();

            templateStream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        }

        try
        {
            var engine = engines.FirstOrDefault(e => e.EngineType == engineType)
                ?? throw new RenderException(request.Slug ?? "ephemeral", engineType.ToString(), $"No render engine registered for '{engineType}'.");

            string dataJson = request.Payload.ValueKind != JsonValueKind.Undefined ? request.Payload.GetRawText() : "{}";

            // Always render to PDF for preview
            return await engine.RenderStreamAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
        }
        finally
        {
            await templateStream.DisposeAsync();
        }
    }
}
