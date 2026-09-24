using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.Engines;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.UseCases.Documents;

/// <summary>
/// Preview use-case: Generates PDF bytes directly for Monaco/Form preview with zero side-effects.
/// Rule: NEVER upload to MinIO, NEVER write to generation_logs, NEVER increment versions.
/// </summary>
public class PreviewDocumentUseCase
{
    private readonly IRepository<Template> _templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo;
    private readonly IStorageService _storageService;
    private readonly IEnumerable<IRenderEngine> _engines;

    public PreviewDocumentUseCase(
        IRepository<Template> templateRepo,
        IRepository<TemplateVersion> versionRepo,
        IStorageService storageService,
        IEnumerable<IRenderEngine> engines)
    {
        _templateRepo = templateRepo;
        _versionRepo = versionRepo;
        _storageService = storageService;
        _engines = engines;
    }

    public async Task<byte[]> ExecuteAsync(string? slug, PreviewDocumentRequest request, CancellationToken ct = default)
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

            var template = await _templateRepo.FirstOrDefaultAsync(t => t.Slug == slug, ct)
                ?? throw new KeyNotFoundException($"Template '{slug}' not found.");

            if (template.CurrentVersionId is null)
                throw new InvalidOperationException($"Template '{slug}' has no published version.");

            var version = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
                ?? throw new InvalidOperationException($"Current version for template '{slug}' not found.");

            engineType = version.GetRenderEngineType();

            templateStream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        }

        try
        {
            var engine = _engines.FirstOrDefault(e => e.EngineType == engineType)
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
