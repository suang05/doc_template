using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Engines;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.UseCases.FieldMappings;

/// <summary>
/// Renders a template with its field mappings applied to caller-supplied sample data.
/// Zero side-effects: no DB writes, no MinIO uploads, no log entries.
/// </summary>
public class PreviewMappingUseCase
{
    private readonly IRepository<Template> _templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo;
    private readonly IRepository<FieldMapping> _mappingRepo;
    private readonly IRepository<TemplateDataset> _tdRepo;
    private readonly IRepository<Dataset> _datasetRepo;
    private readonly IRepository<DataConnection> _connectionRepo;
    private readonly IStorageService _storageService;
    private readonly IEnumerable<IRenderEngine> _engines;
    private readonly IFieldMappingApplicatorService _fieldMappingApplicator;
    private readonly IDataProtectionService _dataProtection;

    public PreviewMappingUseCase(
        IRepository<Template> templateRepo,
        IRepository<TemplateVersion> versionRepo,
        IRepository<FieldMapping> mappingRepo,
        IRepository<TemplateDataset> tdRepo,
        IRepository<Dataset> datasetRepo,
        IRepository<DataConnection> connectionRepo,
        IStorageService storageService,
        IEnumerable<IRenderEngine> engines,
        IFieldMappingApplicatorService fieldMappingApplicator,
        IDataProtectionService dataProtection)
    {
        _templateRepo = templateRepo;
        _versionRepo = versionRepo;
        _mappingRepo = mappingRepo;
        _tdRepo = tdRepo;
        _datasetRepo = datasetRepo;
        _connectionRepo = connectionRepo;
        _storageService = storageService;
        _engines = engines;
        _fieldMappingApplicator = fieldMappingApplicator;
        _dataProtection = dataProtection;
    }

    public async Task<byte[]> ExecuteAsync(Guid templateId, JsonElement sampleData, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new KeyNotFoundException($"Template '{templateId}' not found.");

        var mappings = await _mappingRepo.ListAsync(m => m.TemplateId == templateId, ct);

        string dataJson;
        if (mappings.Count > 0)
        {
            var aliasMap = await DatasetAliasMapBuilder.BuildAsync(
                templateId, _tdRepo, _datasetRepo, _connectionRepo, _dataProtection, ct);
            dataJson = await _fieldMappingApplicator.ApplyAsync(sampleData, mappings, aliasMap);
        }
        else
        {
            dataJson = sampleData.ValueKind != JsonValueKind.Undefined ? sampleData.GetRawText() : "{}";
        }

        if (template.CurrentVersionId is null)
            throw new InvalidOperationException($"Template '{templateId}' has no published version.");

        var version = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new InvalidOperationException($"Current version for template '{templateId}' not found.");

        var engineType = version.GetRenderEngineType();

        var engine = _engines.FirstOrDefault(e => e.EngineType == engineType)
            ?? throw new InvalidOperationException($"No render engine registered for '{engineType}'.");

        using var templateStream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        return await engine.RenderAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
    }
}
