using System.Text.Json;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Services;

public sealed class DataBindingOrchestrator(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo,
    IDataProtectionService dataProtection,
    IFieldMappingApplicatorService fieldMappingApplicator) : IDataBindingOrchestrator
{
    public async Task<string> AssemblePayloadAsync(
        Template template,
        JsonElement callerParameters,
        CancellationToken ct = default)
    {
        var assignments = template.TemplateDatasets.ToList();
        var mappings = template.FieldMappings.ToList();

        // 1. Resolve dataset aliases if template has linked datasets
        var aliasMap = assignments.Count > 0
            ? await DatasetAliasMapBuilder.BuildAsync(assignments, datasetRepo, connectionRepo, dataProtection, ct)
            : new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        // 2. If no mappings and no datasets, pass-through caller parameters untouched
        if (mappings.Count == 0 && aliasMap.Count == 0)
        {
            return callerParameters.ValueKind != JsonValueKind.Undefined
                ? callerParameters.GetRawText()
                : "{}";
        }

        // 3. Coordinate multi-dataset parallel execution and field mapping transformations
        return await fieldMappingApplicator.ApplyAsync(callerParameters, mappings, aliasMap, ct);
    }
}
