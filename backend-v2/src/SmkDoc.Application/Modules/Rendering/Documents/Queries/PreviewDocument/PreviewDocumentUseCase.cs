using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.DataBinding.Services;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Preview use-case: Generates PDF stream directly for Monaco/Form preview with zero side-effects.
/// Supports preview via raw HTML string, template slug, or template UUID.
/// Rule: NEVER upload to MinIO, NEVER write to generation_logs, NEVER increment versions.
/// </summary>
public sealed class PreviewDocumentUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IRenderEngineFactory engineFactory,
    IDataBindingEngine? dataBindingEngine = null) : IUseCase<PreviewDocumentQuery, Stream>
{
    public async Task<Stream> ExecuteAsync(PreviewDocumentQuery query, CancellationToken ct = default)
    {
        Stream templateStream;
        RenderEngineType engineType;
        string dataJson;

        // If Monaco editor supplied the active HTML in the body, use it directly!
        if (!string.IsNullOrWhiteSpace(query.Html))
        {
            templateStream = await HybridSpoolStream.CreateFromUtf8StringAsync(query.Html, ct: ct);
            engineType = RenderEngineType.Html;
            dataJson = query.Payload.ValueKind != JsonValueKind.Undefined ? query.Payload.GetRawText() : "{}";
        }
        else
        {
            Template? template = null;
            if (query.TemplateId.HasValue && query.TemplateId.Value != Guid.Empty)
            {
                var tId = new TemplateId(query.TemplateId.Value);
                template = await templateRepo.GetByIdWithDetailsAsync(tId, ct)
                    ?? await templateRepo.GetByIdAsync(tId, ct);
            }
            else if (!string.IsNullOrWhiteSpace(query.Slug))
            {
                template = await templateRepo.GetBySlugWithDetailsAsync(query.Slug, ct)
                    ?? await templateRepo.GetBySlugAsync(query.Slug, ct);
            }
            else
            {
                throw new DomainValidationException("Template slug, template ID, or HTML content is required for preview.");
            }

            if (template == null)
            {
                var identifier = query.TemplateId?.ToString() ?? query.Slug;
                throw new NotFoundException($"Template '{identifier}' not found.");
            }

            var version = template.GetPublishedVersion();
            engineType = version.GetRenderEngineType();

            if (dataBindingEngine != null)
            {
                var bound = await dataBindingEngine.BindAndValidateAsync(
                    template, version, query.Payload, skipValidation: false, ct);
                dataJson = bound.DataJson;
            }
            else
            {
                dataJson = query.Payload.ValueKind != JsonValueKind.Undefined ? query.Payload.GetRawText() : "{}";
            }

            templateStream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        }

        try
        {
            var engine = engineFactory.GetEngine(engineType);

            // Always render to PDF for preview
            return await engine.RenderStreamAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
        }
        finally
        {
            await templateStream.DisposeAsync();
        }
    }
}
