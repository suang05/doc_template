using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.DataBinding.Helpers;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.DataBinding.Services;

/// <summary>
/// Bounded concurrent executor for SQL datasets with caching and fail-fast cancellation.
/// </summary>
public sealed class DatasetBatchExecutor(
    ISqlExecutorService sqlExecutor,
    IDatasetResultCache? datasetCache = null) : IDatasetBatchExecutor
{
    public const int MaxConcurrentDatasetQueries = 4;
    private static readonly Regex SqlVariablePattern = new(@"@(\w+)", RegexOptions.Compiled);

    public async Task<IReadOnlyDictionary<string, JsonElement>> ExecuteBatchAsync(
        IReadOnlyDictionary<string, ResolvedDatasetContext> datasetAliases,
        IReadOnlyDictionary<string, object?> callerContext,
        Guid? projectId,
        CancellationToken ct = default)
    {
        if (datasetAliases.Count == 0)
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        var aliasCache = new ConcurrentDictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        using var semaphore = new SemaphoreSlim(MaxConcurrentDatasetQueries, MaxConcurrentDatasetQueries);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linkedToken = linkedCts.Token;

        var tasks = datasetAliases.Select(async kvp =>
        {
            var (alias, ds) = (kvp.Key, kvp.Value);
            bool acquired = false;
            try
            {
                await semaphore.WaitAsync(linkedToken);
                acquired = true;

                var parameters = ExtractParameters(ds.SqlQuery, callerContext);
                string? json = null;
                string? cacheKey = null;

                if (ds.CacheSeconds > 0 && datasetCache is not null)
                {
                    cacheKey = DatasetCacheKeyBuilder.BuildKey(projectId, alias, ds, parameters);
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
        return aliasCache;
    }

    public static Dictionary<string, object> ExtractParameters(
        string query,
        IReadOnlyDictionary<string, object?> dict)
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
}

