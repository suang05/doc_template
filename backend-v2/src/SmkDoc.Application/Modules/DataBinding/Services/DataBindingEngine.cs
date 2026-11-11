using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Application.Modules.DataBinding.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Modules.DataBinding.Services;

/// <summary>
/// Unified implementation of <see cref="IDataBindingEngine"/>.
/// Binds incoming caller JSON with external SQL datasets, evaluates transforms and math expressions,
/// and enforces fail-fast Draft-07 schema validation.
/// </summary>
public sealed class DataBindingEngine(
    IDatasetResolver datasetResolver,
    IFieldMappingApplicatorService fieldMappingApplicator,
    IJsonSchemaValidationService schemaValidation,
    TimeProvider timeProvider) : IDataBindingEngine
{
    public async Task<BoundDocumentData> BindAndValidateAsync(
        Template template,
        TemplateVersion version,
        JsonElement rawPayload,
        bool skipValidation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(version);

        var assignments = template.TemplateDatasets.ToList();

        // Template Package / Manifest SSoT:
        // Runtime execution strictly prioritizes the immutable version manifest snapshot over relational tables.
        List<FieldMapping> mappings;
        if (!string.IsNullOrWhiteSpace(version.MappingsSnapshot))
        {
            mappings = FieldMappingSnapshotSerializer.Deserialize(
                template.Id,
                version.MappingsSnapshot,
                timeProvider.GetUtcNow());
        }
        else
        {
            mappings = template.FieldMappings.ToList();
        }

        // 1. Resolve dataset aliases if template has linked datasets
        var aliasMap = assignments.Count > 0
            ? await datasetResolver.ResolveAssignmentsAsync(assignments, ct)
            : new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        // 2. Execute parallel SQL dataset queries and field transformations if mappings or datasets exist
        string dataJson;
        if (mappings.Count == 0 && aliasMap.Count == 0)
        {
            dataJson = rawPayload.ValueKind != JsonValueKind.Undefined
                ? rawPayload.GetRawText()
                : "{}";
        }
        else
        {
            dataJson = await fieldMappingApplicator.ApplyAsync(rawPayload, mappings, aliasMap, ct);
        }

        // 3. Fail-fast JSON Schema validation gate (Draft-07)
        SchemaValidationResult? validationResult = null;
        if (!skipValidation && !string.IsNullOrWhiteSpace(version.DataSchema))
        {
            validationResult = schemaValidation.Validate(version.DataSchema, dataJson);
        }

        return new BoundDocumentData(dataJson, validationResult);
    }
}

