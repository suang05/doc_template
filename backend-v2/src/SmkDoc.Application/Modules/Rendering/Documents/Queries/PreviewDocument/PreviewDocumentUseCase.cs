using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.DataBinding.Services;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Unified live preview use-case: Generates PDF stream directly for Monaco editor, draft files,
/// or published templates with zero side-effects.
/// Supports preview via raw HTML string, draft ID, template slug, or template UUID.
/// Rule: NEVER upload to MinIO output bucket, NEVER write to generation_logs, NEVER increment versions.
/// </summary>
public sealed class PreviewDocumentUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IRenderEngineFactory engineFactory,
    IDataBindingEngine? dataBindingEngine = null,
    ITemplateDraftCache? draftCache = null) : IUseCase<PreviewDocumentQuery, Stream>
{
    public async Task<Stream> ExecuteAsync(PreviewDocumentQuery query, CancellationToken ct)
    {
        var (templateStream, engineType, dataJson) = await PreparePreviewInputAsync(query, ct);

        try
        {
            var engine = engineFactory.GetEngine(engineType);
            return await engine.RenderStreamAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
        }
        finally
        {
            await templateStream.DisposeAsync();
        }
    }

    private async Task<(Stream TemplateStream, RenderEngineType EngineType, string DataJson)> PreparePreviewInputAsync(
        PreviewDocumentQuery query,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(query.Html))
        {
            var htmlStream = await HybridSpoolStream.CreateFromUtf8StringAsync(query.Html, ct: ct);
            return (htmlStream, RenderEngineType.Html, SerializeRawPayload(query.Payload));
        }

        if (!string.IsNullOrWhiteSpace(query.DraftId))
        {
            return await PrepareDraftPreviewAsync(query, ct);
        }

        return await PreparePublishedTemplatePreviewAsync(query, ct);
    }

    private async Task<(Stream TemplateStream, RenderEngineType EngineType, string DataJson)> PrepareDraftPreviewAsync(
        PreviewDocumentQuery query,
        CancellationToken ct)
    {
        if (draftCache is null)
        {
            throw new BusinessRuleViolationException("Draft cache is not available for preview.", "DRAFT_CACHE_UNAVAILABLE");
        }

        var draft = await draftCache.GetCurrentProjectDraftAsync(query.DraftId!, ct)
            ?? throw new DraftExpiredException(query.DraftId!);

        if (!TemplateFormat.TryFromExtension(draft.FileExtension, out var format))
        {
            throw new DomainValidationException($"Unsupported template file format: '{draft.FileExtension}'.");
        }

        var draftStream = await draftCache.OpenStreamAsync(draft, ct);
        return (draftStream, format.DefaultEngineType, SerializeRawPayload(query.Payload));
    }

    private async Task<(Stream TemplateStream, RenderEngineType EngineType, string DataJson)> PreparePublishedTemplatePreviewAsync(
        PreviewDocumentQuery query,
        CancellationToken ct)
    {
        var template = await FindTemplateAsync(query, ct);
        var version = template.GetPublishedVersion();
        var dataJson = dataBindingEngine != null
            ? (await dataBindingEngine.BindAndValidateAsync(template, version, query.Payload, skipValidation: false, ct)).DataJson
            : SerializeRawPayload(query.Payload);

        var templateStream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        return (templateStream, version.RenderEngineType, dataJson);
    }

    private async Task<Template> FindTemplateAsync(PreviewDocumentQuery query, CancellationToken ct)
    {
        Template? template;
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
            throw new DomainValidationException("Template slug, template ID, draft ID, or HTML content is required for preview.");
        }

        if (template == null)
        {
            var identifier = query.TemplateId?.ToString() ?? query.Slug;
            throw new NotFoundException($"Template '{identifier}' not found.");
        }

        return template;
    }

    private static string SerializeRawPayload(JsonElement payload) =>
        payload.ValueKind != JsonValueKind.Undefined ? payload.GetRawText() : "{}";
}
