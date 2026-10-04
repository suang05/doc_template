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
    private readonly IDatasetRepository _datasetRepo = datasetRepo;
    private readonly IDataConnectionRepository _connectionRepo = connectionRepo;
    private readonly IDataProtectionService _dataProtection = dataProtection;
    private readonly IFieldMappingApplicatorService _fieldMappingApplicator = fieldMappingApplicator;
    private readonly IJsonSchemaValidationService _schemaValidation = schemaValidation;

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
                template.TemplateDatasets, _datasetRepo, _connectionRepo, _dataProtection, ct);
            dataJson = await _fieldMappingApplicator.ApplyAsync(rawData, mappings, aliasMap);
        }
        else
        {
            dataJson = rawData.ValueKind != JsonValueKind.Undefined ? rawData.GetRawText() : "{}";
        }

        // Schema Validation Gate (Draft-07)
        SchemaValidationResult? validationResult = null;
        if (!skipValidation && !string.IsNullOrWhiteSpace(currentVersion.DataSchema))
        {
            validationResult = _schemaValidation.Validate(currentVersion.DataSchema, dataJson);
        }

        return new PreparedDocumentData(dataJson, validationResult);
    }
}
