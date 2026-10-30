using System.Text.Json;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentDataPreparationService(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo,
    IDataProtectionService dataProtection,
    IFieldMappingApplicatorService fieldMappingApplicator,
    IJsonSchemaValidationService schemaValidation) : IDocumentDataPreparationService
{
    public async Task<PreparedDocumentData> PrepareDataAsync(
        Template template,
        TemplateVersion currentVersion,
        JsonElement rawData,
        bool skipValidation,
        CancellationToken ct = default)
    {
        var mappings = template.FieldMappings.ToList();
        string dataJson;

        if (mappings.Count > 0)
        {
            var aliasMap = await DatasetAliasMapBuilder.BuildAsync(
                template.TemplateDatasets, datasetRepo, connectionRepo, dataProtection, ct);
            dataJson = await fieldMappingApplicator.ApplyAsync(rawData, mappings, aliasMap, ct);
        }
        else
        {
            dataJson = rawData.ValueKind != JsonValueKind.Undefined ? rawData.GetRawText() : "{}";
        }

        // Schema Validation Gate (Draft-07)
        SchemaValidationResult? validationResult = null;
        if (!skipValidation && !string.IsNullOrWhiteSpace(currentVersion.DataSchema))
        {
            validationResult = schemaValidation.Validate(currentVersion.DataSchema, dataJson);
        }

        return new PreparedDocumentData(dataJson, validationResult);
    }
}
