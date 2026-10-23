using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;

/// <summary>
/// Renders a template with its field mappings applied to caller-supplied sample data.
/// Zero side-effects: no DB writes, no MinIO uploads, no log entries.
/// </summary>
public class PreviewMappingUseCase(
    ITemplateRepository templateRepo,
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo,
    IStorageService storageService,
    IEnumerable<IRenderEngine> engines,
    IFieldMappingApplicatorService fieldMappingApplicator,
    IDataProtectionService dataProtection)
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IDatasetRepository _datasetRepo = datasetRepo;
    private readonly IDataConnectionRepository _connectionRepo = connectionRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IEnumerable<IRenderEngine> _engines = engines;
    private readonly IFieldMappingApplicatorService _fieldMappingApplicator = fieldMappingApplicator;
    private readonly IDataProtectionService _dataProtection = dataProtection;

    public async Task<Stream> ExecuteStreamAsync(Guid templateId, JsonElement sampleData, CancellationToken ct = default)
    {
        var (version, dataJson, engine) = await PrepareRenderAsync(templateId, sampleData, ct);

        var templateStream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        try
        {
            return await engine.RenderStreamAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
        }
        finally
        {
            templateStream.Dispose();
        }
    }

    public async Task<byte[]> ExecuteAsync(Guid templateId, JsonElement sampleData, CancellationToken ct = default)
    {
        var (version, dataJson, engine) = await PrepareRenderAsync(templateId, sampleData, ct);

        using var templateStream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        return await engine.RenderAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
    }

    private async Task<(TemplateVersion Version, string DataJson, IRenderEngine Engine)> PrepareRenderAsync(
        Guid templateId, JsonElement sampleData, CancellationToken ct)
    {
        var tId = new TemplateId(templateId);
        var template = await _templateRepo.GetByIdWithDetailsAsync(tId, ct)
            ?? await _templateRepo.GetByIdAsync(tId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        var mappings = template.FieldMappings.ToList();

        string dataJson;
        if (mappings.Count > 0)
        {
            var aliasMap = await DatasetAliasMapBuilder.BuildAsync(
                template.TemplateDatasets, _datasetRepo, _connectionRepo, _dataProtection, ct);
            dataJson = await _fieldMappingApplicator.ApplyAsync(sampleData, mappings, aliasMap);
        }
        else
        {
            dataJson = sampleData.ValueKind != JsonValueKind.Undefined ? sampleData.GetRawText() : "{}";
        }

        if (template.CurrentVersionId is null)
            throw new BusinessRuleViolationException($"Template '{templateId}' has no published version.");

        var version = template.CurrentVersion
            ?? await _templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Current version for template '{templateId}' not found.");

        var engineType = version.GetRenderEngineType();

        var engine = _engines.FirstOrDefault(e => e.EngineType == engineType)
            ?? throw new RenderException(templateId.ToString(), engineType.ToString(), $"No render engine registered for '{engineType}'.");

        return (version, dataJson, engine);
    }
}
