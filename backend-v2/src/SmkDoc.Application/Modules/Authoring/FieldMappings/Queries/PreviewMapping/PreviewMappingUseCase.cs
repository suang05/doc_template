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
/// Pure Stream output (Zero LOH).
/// </summary>
public sealed class PreviewMappingUseCase(
    ITemplateRepository templateRepo,
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo,
    IStorageService storageService,
    IRenderEngineFactory engineFactory,
    IFieldMappingApplicatorService fieldMappingApplicator,
    IDataProtectionService dataProtection) : IUseCase<PreviewMappingQuery, Stream>
{
    public async Task<Stream> ExecuteAsync(PreviewMappingQuery query, CancellationToken ct = default)
    {
        var (version, dataJson, engine) = await PrepareRenderAsync(query.TemplateId, query.SampleData, ct);

        var templateStream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        try
        {
            return await engine.RenderStreamAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
        }
        finally
        {
            await templateStream.DisposeAsync();
        }
    }

    private async Task<(TemplateVersion Version, string DataJson, IRenderEngine Engine)> PrepareRenderAsync(
        Guid templateId, JsonElement sampleData, CancellationToken ct)
    {
        var tId = new TemplateId(templateId);
        var template = await templateRepo.GetByIdWithDetailsAsync(tId, ct)
            ?? await templateRepo.GetByIdAsync(tId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        var mappings = template.FieldMappings.ToList();

        string dataJson;
        if (mappings.Count > 0)
        {
            var aliasMap = await DatasetAliasMapBuilder.BuildAsync(
                template.TemplateDatasets, datasetRepo, connectionRepo, dataProtection, ct);
            dataJson = await fieldMappingApplicator.ApplyAsync(sampleData, mappings, aliasMap, ct);
        }
        else
        {
            dataJson = sampleData.ValueKind != JsonValueKind.Undefined ? sampleData.GetRawText() : "{}";
        }

        var version = template.GetPublishedVersion();
        var engineType = version.GetRenderEngineType();
        IRenderEngine engine;
        try
        {
            engine = engineFactory.GetEngine(engineType);
        }
        catch (NotSupportedException ex)
        {
            throw new RenderException(templateId.ToString(), engineType.ToString(), ex.Message, ex);
        }

        return (version, dataJson, engine);
    }
}
