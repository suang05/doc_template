using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Common.Helpers;

public sealed class FieldMappingApplicatorService(
    ISqlExecutorService sqlExecutor,
    IMathExpressionResolver mathResolver,
    IDatasetResultCache? datasetCache = null,
    ICurrentProjectContext? projectContext = null) : IFieldMappingApplicatorService
{
    public const int MaxConcurrentDatasetQueries = 4;

    private static readonly Regex SqlVariablePattern = new(@"@(\w+)", RegexOptions.Compiled);
    private static readonly Regex JsonPathArrayPattern = new(@"^([a-zA-Z0-9_-]+)\[(\d+)\]$", RegexOptions.Compiled);

    public Task<string> ApplyAsync(
        JsonElement root,
        IEnumerable<FieldMapping> mappings,
        IReadOnlyDictionary<string, ResolvedDatasetContext> datasetAliases) =>
        ApplyAsync(root, mappings, datasetAliases, default);

    public async Task<string> ApplyAsync(
        JsonElement root,
        IEnumerable<FieldMapping> mappings,
        IReadOnlyDictionary<string, ResolvedDatasetContext> datasetAliases,
        CancellationToken ct)
    {
        var dict = new Dictionary<string, object?>();

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
                dict[prop.Name] = prop.Value.Clone();
        }

        var ordered = mappings.OrderBy(m => m.SortOrder).ToList();

        // -- Pass 1: json --------------------------------------------------
        foreach (var mapping in ordered.Where(m => m.DataSourceType == DataSourceType.Json))
        {
            var val = Finalize(ResolveJsonPath(root, mapping.SourcePath), mapping);
            dict[mapping.Placeholder] = val;
        }

        // -- Pass 1b: sql -- bounded parallel execution per distinct alias --
        var sqlMappings = ordered
            .Where(m => m.DataSourceType == DataSourceType.Sql && m.DatasetAlias != null)
            .ToList();

        var distinctAliases = sqlMappings
            .Select(m => m.DatasetAlias!.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(datasetAliases.ContainsKey)
            .ToList();

        var aliasCache = new ConcurrentDictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (distinctAliases.Count > 0)
        {
            using var semaphore = new SemaphoreSlim(MaxConcurrentDatasetQueries, MaxConcurrentDatasetQueries);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var linkedToken = linkedCts.Token;

            var tasks = distinctAliases.Select(async alias =>
            {
                var ds = datasetAliases[alias];
                bool acquired = false;
                try
                {
                    await semaphore.WaitAsync(linkedToken);
                    acquired = true;

                    var parameters = ExtractParameters(ds.SqlQuery, dict);
                    string? json = null;
                    string? cacheKey = null;

                    if (ds.CacheSeconds > 0 && datasetCache is not null)
                    {
                        cacheKey = BuildDatasetCacheKey(projectContext, alias, ds, parameters);
                        json = await datasetCache.GetAsync(cacheKey, linkedToken);
                    }

                    if (json is null)
                    {
                        json = await sqlExecutor.ExecuteQueryAsJsonAsync(
                            provider: ds.Provider,
                            connectionString: ds.ConnectionString,
                            sqlQuery: ds.SqlQuery,
                            parameters: parameters,
                            ct: linkedToken);

                        if (cacheKey is not null && datasetCache is not null && !string.IsNullOrWhiteSpace(json))
                        {
                            await datasetCache.SetAsync(
                                cacheKey,
                                json,
                                TimeSpan.FromSeconds(ds.CacheSeconds),
                                linkedToken);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        using var parsedDoc = JsonDocument.Parse(json);
                        aliasCache[alias] = parsedDoc.RootElement.Clone();
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
                {
                    // Aborted due to fail-fast cancellation from a faulted sibling dataset query.
                }
                catch (Exception ex) when (ex is DatasetExecutionException or ForbiddenException)
                {
                    await linkedCts.CancelAsync();
                    throw;
                }
                catch (Exception ex)
                {
                    await linkedCts.CancelAsync();
                    throw new DatasetExecutionException(alias, ds.Provider, ex.Message, ex);
                }
                finally
                {
                    if (acquired)
                    {
                        semaphore.Release();
                    }
                }
            });

            await Task.WhenAll(tasks);
        }

        // Resolve per-field ResultPath from cached alias result
        foreach (var mapping in sqlMappings)
        {
            if (mapping.DatasetAlias != null && aliasCache.TryGetValue(mapping.DatasetAlias.Value, out var resultEl))
            {
                if (string.IsNullOrWhiteSpace(mapping.ResultPath) && (resultEl.ValueKind == JsonValueKind.Array || resultEl.ValueKind == JsonValueKind.Object))
                {
                    dict[mapping.Placeholder] = resultEl.Clone();
                    continue;
                }

                string? val = string.IsNullOrWhiteSpace(mapping.ResultPath)
                    ? ExtractFirstScalar(resultEl)
                    : ResolveJsonPath(resultEl, mapping.ResultPath);

                dict[mapping.Placeholder] = Finalize(val, mapping);
            }
            else
            {
                dict[mapping.Placeholder] = Finalize(null, mapping);
            }
        }

        // Auto-bind dataset aliases (SSRS/JasperReports model: datasets exposed directly by alias)
        foreach (var (alias, resultEl) in aliasCache)
        {
            if (!dict.ContainsKey(alias))
            {
                dict[alias] = resultEl.Clone();
            }
        }

        // -- Pass 2: MathExpression -- applied after all json/sql settled ---
        var stringVars = BuildStringVars(dict);
        foreach (var mapping in ordered.Where(m => !string.IsNullOrWhiteSpace(m.MathExpression)))
        {
            var val = mathResolver.Resolve(mapping.MathExpression!, stringVars);

            if (string.IsNullOrWhiteSpace(val)) val = mapping.DefaultValue ?? string.Empty;

            if (mapping.Required && string.IsNullOrWhiteSpace(val))
                throw new DomainValidationException(
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

    private static string BuildDatasetCacheKey(
        ICurrentProjectContext? projectContext,
        string alias,
        ResolvedDatasetContext ds,
        Dictionary<string, object> parameters)
    {
        var tenantSegment = projectContext is { IsResolved: true }
            ? projectContext.ProjectId.Value.ToString("N")
            : "unscoped";
        var datasetSegment = ds.DatasetId != Guid.Empty
            ? ds.DatasetId.ToString("N")
            : alias.ToLowerInvariant();
        var connectionSegment = ds.DataConnectionId != Guid.Empty
            ? ds.DataConnectionId.ToString("N")
            : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ds.ConnectionString)))[..16];

        var sb = new StringBuilder(256);
        sb.Append(ds.Provider).Append('\n').Append(ds.SqlQuery);
        foreach (var kvp in parameters.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append('\n')
              .Append(kvp.Key.ToLowerInvariant())
              .Append('=')
              .Append(kvp.Value is DBNull ? "<null>" : Convert.ToString(kvp.Value, CultureInfo.InvariantCulture));
        }

        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
        return $"dataset:{tenantSegment}:{datasetSegment}:{connectionSegment}:{fingerprint}";
    }

    private static string Finalize(string? val, FieldMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(val)) val = mapping.DefaultValue;

        if (mapping.Required && string.IsNullOrWhiteSpace(val))
            throw new DomainValidationException(
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
                string s => s,
                JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText(),
                null => null,
                _ => value.ToString()
            };
        }
        return result;
    }

    private static Dictionary<string, object> ExtractParameters(string query, Dictionary<string, object?> dict)
    {
        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var matches = SqlVariablePattern.Matches(query);

        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value;
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                object resolvedVal = val switch
                {
                    JsonElement je => je.ValueKind switch
                    {
                        JsonValueKind.String => je.GetString() ?? string.Empty,
                        JsonValueKind.Number => je.TryGetInt64(out var l) ? l : je.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => DBNull.Value,
                        _ => je.GetRawText()
                    },
                    _ => val
                };

                parameters[key] = resolvedVal;
            }
        }

        return parameters;
    }

    private static string? ResolveJsonPath(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var parts = path.Split('.');
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
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => current.GetRawText()
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
                JsonValueKind.String => first.Value.GetString(),
                JsonValueKind.Undefined => null,
                _ => first.Value.GetRawText()
            };
        }

        return target.ValueKind switch
        {
            JsonValueKind.String => target.GetString(),
            JsonValueKind.Number => target.GetRawText(),
            _ => null
        };
    }
}
