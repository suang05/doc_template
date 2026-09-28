using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Preview use-case: Generates PDF bytes directly for Monaco/Form preview with zero side-effects.
/// Rule: NEVER upload to MinIO, NEVER write to generation_logs, NEVER increment versions.
/// </summary>
public sealed class PreviewDocumentUseCase(
    IRepository<Template> templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IStorageService storageService,
    IEnumerable<IRenderEngine> engines)
{
    public async Task<byte[]> ExecuteAsync(string? slug, PreviewDocumentQuery request, CancellationToken ct = default)
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
            if (string.IsNullOrWhiteSpace(slug))
                throw new ArgumentException("Template slug or HTML content is required for preview.");

            var template = await templateRepo.FirstOrDefaultAsync(t => t.Slug == slug, ct)
                ?? throw new NotFoundException($"Template '{slug}' not found.");

            if (template.CurrentVersionId is null)
                throw new InvalidOperationException($"Template '{slug}' has no published version.");

            var version = await versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
                ?? throw new InvalidOperationException($"Current version for template '{slug}' not found.");

            engineType = version.GetRenderEngineType();

            templateStream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        }

        try
        {
            var engine = engines.FirstOrDefault(e => e.EngineType == engineType)
                ?? throw new InvalidOperationException($"No render engine registered for '{engineType}'.");

            string dataJson = request.Data.ValueKind != JsonValueKind.Undefined ? request.Data.GetRawText() : "{}";

            // Always render to PDF for preview
            return await engine.RenderAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
        }
        finally
        {
            await templateStream.DisposeAsync();
        }
    }
}
