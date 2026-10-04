using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Templates;

public sealed class HtmlStudioUseCase(
    ITemplateRepository templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IStorageService storageService,
    IEnumerable<IRenderEngine> renderEngines,
    ITemplateScannerService scannerService) : IHtmlStudioUseCase
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IRenderEngine _htmlEngine = renderEngines.First(e => e.EngineType == RenderEngineType.Html);
    private readonly ITemplateScannerService _scannerService = scannerService;

    public async Task<string> GetHtmlSourceAsync(Guid templateId, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        if (template.CurrentVersionId == null)
            throw new InvalidOperationException($"Template '{templateId}' has no active published version.");

        var version = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Active version for template '{templateId}' not found.");

        if (version.FileFormat != null && version.FileFormat != TemplateFormat.Html)
            throw new InvalidOperationException("Only HTML templates can be opened in the Monaco editor.");

        using var stream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task<TemplateStudioDto> GetStudioBundleAsync(Guid templateId, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        if (template.CurrentVersionId == null)
            throw new InvalidOperationException($"Template '{templateId}' has no active published version.");

        var version = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Active version for template '{templateId}' not found.");

        if (version.FileFormat != null && version.FileFormat != TemplateFormat.Html)
            throw new InvalidOperationException("Only HTML templates can be opened in the Monaco editor.");

        using var stream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string html = await reader.ReadToEndAsync(ct);

        return new TemplateStudioDto(
            Html: html,
            SamplePayload: version.SamplePayload,
            DataSchema: version.DataSchema,
            Version: version.Version
        );
    }

    public async Task<TemplateSchemaDto> GetTemplateSchemaAsync(Guid templateId, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        TemplateVersion? activeVersion = null;
        if (template.CurrentVersionId.HasValue)
        {
            activeVersion = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct);
        }

        return new TemplateSchemaDto(
            TemplateId: template.Id,
            Slug: template.Slug,
            Format: activeVersion?.FileFormat,
            Version: activeVersion?.Version ?? 1,
            DataSchema: activeVersion?.DataSchema,
            SamplePayload: activeVersion?.SamplePayload
        );
    }

    public async Task<byte[]> PreviewHtmlBufferAsync(string htmlContent, string sampleDataJson, CancellationToken ct = default)
    {
        string safeData = string.IsNullOrWhiteSpace(sampleDataJson) ? "{}" : sampleDataJson;
        byte[] htmlBytes = Encoding.UTF8.GetBytes(htmlContent ?? string.Empty);

        using var templateStream = new MemoryStream(htmlBytes);
        return await _htmlEngine.RenderAsync(templateStream, safeData, OutputFormat.Pdf, ct);
    }

    public async Task<TemplateValidationResultDto> ValidateHtmlAsync(string htmlContent, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent ?? string.Empty));
        var placeholders = await _scannerService.ScanPlaceholdersAsync(stream, ".html", ct);
        return new TemplateValidationResultDto(
            Valid: true,
            Fields: placeholders,
            Errors: new List<string>()
        );
    }
}
