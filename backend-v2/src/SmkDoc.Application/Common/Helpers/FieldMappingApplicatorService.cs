using System.Text.Json;
using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.FieldMappings;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Common.Helpers;

public sealed class FieldMappingApplicatorService(
    ISqlExecutorService sqlExecutor,
    IMathExpressionResolver mathResolver) : IFieldMappingApplicatorService
{
    private static readonly Regex SqlVariablePattern = new(@"@(\w+)", RegexOptions.Compiled);
    private static readonly Regex JsonPathArrayPattern = new(@"^([a-zA-Z0-9_-]+)\[(\d+)\]$", RegexOptions.Compiled);

    public async Task<string> ApplyAsync(
        JsonElement root,
        IEnumerable<FieldMapping> mappings,
        IReadOnlyDictionary<string, ResolvedDatasetContext> datasetAliases)
    {
        var dict = new Dictionary<string, object?>();

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
                dict[prop.Name] = prop.Value.Clone();
        }

        var ordered = mappings.OrderBy(m => m.SortOrder).ToList();

        // ── Pass 1: json ──────────────────────────────────────────────────
        foreach (var mapping in ordered.Where(m => m.DataSourceType == DataSourceType.Json))
        {
            var val = Finalize(ResolveJsonPath(root, mapping.SourcePath), mapping);
            dict[mapping.Placeholder] = val;
        }

        // ── Pass 1b: sql — group by alias, execute each query once ────────
        var sqlMappings = ordered
            .Where(m => m.DataSourceType == DataSourceType.Sql && !string.IsNullOrWhiteSpace(m.DatasetAlias))
            .ToList();

        // Cache: alias → parsed JSON result
        var aliasCache = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var alias in sqlMappings.Select(m => m.DatasetAlias!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!datasetAliases.TryGetValue(alias, out var ds)) continue;

            try
            {
                var query = ReplaceVariables(ds.SqlQuery, dict);
                var json  = await sqlExecutor.ExecuteQueryAsJsonAsync(ds.Provider, ds.ConnectionString, query);

                if (!string.IsNullOrWhiteSpace(json))
                    aliasCache[alias] = JsonDocument.Parse(json).RootElement.Clone();
            }
            catch { /* leave cache empty for this alias */ }
        }

        // Resolve per-field ResultPath from cached alias result
        foreach (var mapping in sqlMappings)
        {
            string? val = null;
            if (aliasCache.TryGetValue(mapping.DatasetAlias!, out var resultEl))
            {
                val = string.IsNullOrWhiteSpace(mapping.ResultPath)
                    ? ExtractFirstScalar(resultEl)
                    : ResolveJsonPath(resultEl, mapping.ResultPath);
            }
            dict[mapping.Placeholder] = Finalize(val, mapping);
        }

        // ── Pass 2: MathExpression — applied after all json/sql settled ───
        var stringVars = BuildStringVars(dict);
        foreach (var mapping in ordered.Where(m => !string.IsNullOrWhiteSpace(m.MathExpression)))
        {
            var val = mathResolver.Resolve(mapping.MathExpression!, stringVars);

            if (string.IsNullOrWhiteSpace(val)) val = mapping.DefaultValue ?? string.Empty;

            if (mapping.Required && string.IsNullOrWhiteSpace(val))
                throw new InvalidOperationException(
                    $"Required field '{mapping.Label}' ({mapping.Placeholder}) is missing.");

            if (!string.IsNullOrWhiteSpace(val) && !string.IsNullOrWhiteSpace(mapping.Transform))
                val = ThaiDataTransformer.Transform(val, mapping.Transform);

            dict[mapping.Placeholder] = val;
            stringVars[mapping.Placeholder] = val;
        }

        return JsonSerializer.Serialize(dict, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string Finalize(string? val, FieldMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(val)) val = mapping.DefaultValue;

        if (mapping.Required && string.IsNullOrWhiteSpace(val))
            throw new InvalidOperationException(
                $"Required field '{mapping.Label}' ({mapping.Placeholder}) is missing.");

        if (!string.IsNullOrWhiteSpace(val) && !string.IsNullOrWhiteSpace(mapping.Transform))
            val = ThaiDataTransformer.Transform(val, mapping.Transform);

        return val ?? string.Empty;
    }

    private static Dictionary<string, string?> BuildStringVars(Dictionary<string, object?> dict)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in dict)
        {
            result[key] = value switch
            {
                string s       => s,
                JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText(),
                null           => null,
                _              => value.ToString()
            };
        }
        return result;
    }

    private static string ReplaceVariables(string query, Dictionary<string, object?> dict)
    {
        return SqlVariablePattern.Replace(query, m =>
        {
            var key = m.Groups[1].Value;
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                var s = val.ToString();
                return $"'{s?.Replace("'", "''")}'";
            }
            return m.Value;
        });
    }

    private static string? ResolveJsonPath(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var parts   = path.Split('.');
        var current = root;

        foreach (var part in parts)
        {
            var match = JsonPathArrayPattern.Match(part);
            if (match.Success)
            {
                var propName = match.Groups[1].Value;
                if (current.ValueKind == JsonValueKind.Object
                    && current.TryGetProperty(propName, out var prop)
                    && prop.ValueKind == JsonValueKind.Array)
                {
                    if (int.TryParse(match.Groups[2].Value, out int index) && index >= 0 && index < prop.GetArrayLength())
                        current = prop[index];
                    else
                        return null;
                }
                else return null;
            }
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(part, out var idx))
            {
                if (idx < 0 || idx >= current.GetArrayLength()) return null;
                current = current[idx];
            }
            else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var prop))
            {
                current = prop;
            }
            else return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True   => "true",
            JsonValueKind.False  => "false",
            JsonValueKind.Null   => string.Empty,
            _                    => current.GetRawText()
        };
    }

    private static string? ExtractFirstScalar(JsonElement el)
    {
        var target = el.ValueKind == JsonValueKind.Array && el.GetArrayLength() > 0
            ? el[0]
            : el;

        if (target.ValueKind == JsonValueKind.Object)
        {
            var first = target.EnumerateObject().FirstOrDefault();
            return first.Value.ValueKind switch
            {
                JsonValueKind.String    => first.Value.GetString(),
                JsonValueKind.Undefined => null,
                _                       => first.Value.GetRawText()
            };
        }

        return target.ValueKind switch
        {
            JsonValueKind.String => target.GetString(),
            JsonValueKind.Number => target.GetRawText(),
            _                    => null
        };
    }
}
