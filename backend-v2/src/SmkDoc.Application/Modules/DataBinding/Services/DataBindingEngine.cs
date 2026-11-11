using System.Text.Json;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Application.Modules.DataBinding.DTOs;
using SmkDoc.Application.Modules.DataBinding.Helpers;
using SmkDoc.Application.Modules.Integration.Datasets.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Modules.DataBinding.Services;

/// <summary>
/// Authoritative implementation of <see cref="IDataBindingEngine"/>.
/// Binds incoming caller JSON with external SQL datasets, evaluates transforms and math expressions,
/// and enforces fail-fast Draft-07 schema validation without intermediate wrapper indirection.
/// </summary>
public sealed class DataBindingEngine(
    IDatasetResolver datasetResolver,
    IDatasetBatchExecutor datasetExecutor,
    IMathExpressionResolver mathResolver,
    IJsonSchemaValidationService schemaValidation,
    TimeProvider timeProvider) : IDataBindingEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<BoundDocumentData> BindAndValidateAsync(
        Template template,
        TemplateVersion version,
        JsonElement rawPayload,
        bool skipValidation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(version);

        var mappings = ResolveMappings(template, version);
        var aliasMap = template.TemplateDatasets.Count > 0
            ? await datasetResolver.ResolveAssignmentsAsync(template.TemplateDatasets, ct)
            : new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        var boundDict = InitializePayloadDict(rawPayload);
        BindJsonMappings(rawPayload, mappings, boundDict);

        if (aliasMap.Count > 0)
        {
            var aliasResults = await datasetExecutor.ExecuteBatchAsync(aliasMap, boundDict, template.ProjectId.Value, ct);
            BindSqlMappings(aliasResults, mappings, boundDict);
        }

        EvaluateMathExpressions(mappings, boundDict);

        string dataJson = SerializePayload(boundDict, rawPayload);
        var validation = ValidateSchema(version.DataSchema, dataJson, skipValidation);
        return new BoundDocumentData(dataJson, validation);
    }

    private static string SerializePayload(Dictionary<string, object?> boundDict, JsonElement rawPayload) =>
        boundDict.Count > 0
            ? JsonSerializer.Serialize(boundDict, JsonOptions)
            : (rawPayload.ValueKind != JsonValueKind.Undefined ? rawPayload.GetRawText() : "{}");

    private SchemaValidationResult? ValidateSchema(string? schema, string dataJson, bool skipValidation) =>
        !skipValidation && !string.IsNullOrWhiteSpace(schema)
            ? schemaValidation.Validate(schema, dataJson)
            : null;

    private List<FieldMapping> ResolveMappings(Template template, TemplateVersion version)
    {
        if (!string.IsNullOrWhiteSpace(version.MappingsSnapshot))
        {
            return FieldMappingSnapshotSerializer.Deserialize(template.Id, version.MappingsSnapshot, timeProvider.GetUtcNow());
        }
        return template.FieldMappings.ToList();
    }

    private static Dictionary<string, object?> InitializePayloadDict(JsonElement rawPayload)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (rawPayload.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in rawPayload.EnumerateObject())
                dict[prop.Name] = prop.Value.Clone();
        }
        return dict;
    }

    private static void BindJsonMappings(JsonElement root, List<FieldMapping> mappings, Dictionary<string, object?> dict)
    {
        foreach (var m in mappings.Where(m => m.DataSourceType == DataSourceType.Json && string.IsNullOrWhiteSpace(m.MathExpression)).OrderBy(m => m.SortOrder))
        {
            dict[m.Placeholder] = FinalizeValue(JsonPathEvaluator.ResolvePath(root, m.SourcePath), m);
        }
    }

    private static void BindSqlMappings(
        IReadOnlyDictionary<string, JsonElement> aliasResults,
        List<FieldMapping> mappings,
        Dictionary<string, object?> dict)
    {
        foreach (var m in mappings.Where(m => m.DataSourceType == DataSourceType.Sql && m.DatasetAlias != null).OrderBy(m => m.SortOrder))
        {
            ApplySqlMapping(m, aliasResults, dict);
        }

        foreach (var (alias, el) in aliasResults)
        {
            if (!dict.ContainsKey(alias)) dict[alias] = el.Clone();
        }
    }

    private static void ApplySqlMapping(
        FieldMapping m,
        IReadOnlyDictionary<string, JsonElement> aliasResults,
        Dictionary<string, object?> dict)
    {
        if (aliasResults.TryGetValue(m.DatasetAlias!.Value, out var el))
        {
            if (string.IsNullOrWhiteSpace(m.ResultPath) && (el.ValueKind == JsonValueKind.Array || el.ValueKind == JsonValueKind.Object))
            {
                dict[m.Placeholder] = el.Clone();
                return;
            }
            var val = string.IsNullOrWhiteSpace(m.ResultPath) ? JsonPathEvaluator.ExtractFirstScalar(el) : JsonPathEvaluator.ResolvePath(el, m.ResultPath);
            dict[m.Placeholder] = FinalizeValue(val, m);
        }
        else
        {
            dict[m.Placeholder] = FinalizeValue(null, m);
        }
    }

    private void EvaluateMathExpressions(List<FieldMapping> mappings, Dictionary<string, object?> dict)
    {
        var stringVars = BuildStringVars(dict);
        foreach (var m in mappings.Where(m => !string.IsNullOrWhiteSpace(m.MathExpression)).OrderBy(m => m.SortOrder))
        {
            var val = mathResolver.Resolve(m.MathExpression!, stringVars);
            val = FinalizeValue(string.IsNullOrWhiteSpace(val) ? m.DefaultValue : val, m);
            dict[m.Placeholder] = val;
            stringVars[m.Placeholder] = val;
        }
    }

    private static string FinalizeValue(string? val, FieldMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(val)) val = mapping.DefaultValue;

        if (mapping.Required && string.IsNullOrWhiteSpace(val))
            throw new DomainValidationException($"Required field '{mapping.Label}' ({mapping.Placeholder}) is missing.");

        if (!string.IsNullOrWhiteSpace(val) && !string.IsNullOrWhiteSpace(mapping.Transform))
            val = ThaiDataTransformer.Transform(val, mapping.Transform);

        return val ?? string.Empty;
    }

    private static Dictionary<string, string?> BuildStringVars(Dictionary<string, object?> dict)
    {
        var vars = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in dict)
        {
            vars[k] = v switch
            {
                string s => s,
                JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText(),
                null => null,
                _ => v.ToString()
            };
        }
        return vars;
    }
}
