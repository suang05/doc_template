using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.DataBinding.Helpers;

namespace SmkDoc.Application.Modules.DataBinding.Services;

/// <summary>
/// Bounded concurrent executor for SQL datasets with caching and fail-fast cancellation.
/// </summary>
public sealed partial class DatasetBatchExecutor(
    ISqlExecutorService sqlExecutor,
    IDatasetResultCache? datasetCache = null) : IDatasetBatchExecutor
{
    public const int MaxConcurrentDatasetQueries = 4;

    [GeneratedRegex(@"@(\w+)", RegexOptions.None, 1000)]
    private static partial Regex SqlVariablePattern();

    public async Task<IReadOnlyDictionary<string, JsonElement>> ExecuteBatchAsync(
        IReadOnlyDictionary<string, ResolvedDatasetContext> datasetAliases,
        IReadOnlyDictionary<string, object?> callerContext,
        Guid? projectId,
        CancellationToken ct = default)
    {
        if (datasetAliases.Count == 0) return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        var aliasCache = new ConcurrentDictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var semaphore = new SemaphoreSlim(MaxConcurrentDatasetQueries, MaxConcurrentDatasetQueries);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            var tasks = datasetAliases.Select(kvp =>
                ExecuteSingleQueryWithGuardAsync(kvp.Key, kvp.Value, callerContext, projectId, semaphore, linkedCts, aliasCache, ct))
                .ToArray();

            await Task.WhenAll(tasks);
            return aliasCache;
        }
        finally
        {
            linkedCts.Dispose();
            semaphore.Dispose();
        }
    }

    private async Task ExecuteSingleQueryWithGuardAsync(
        string alias,
        ResolvedDatasetContext ds,
        IReadOnlyDictionary<string, object?> callerContext,
        Guid? projectId,
        SemaphoreSlim semaphore,
        CancellationTokenSource linkedCts,
        ConcurrentDictionary<string, JsonElement> aliasCache,
        CancellationToken ct)
    {
        var linkedToken = linkedCts.Token;
        bool acquired = false;
        try
        {
            await semaphore.WaitAsync(linkedToken);
            acquired = true;

            var parameters = ExtractParameters(ds.SqlQuery, callerContext);
            var json = await FetchOrExecuteQueryAsync(projectId, alias, ds, parameters, linkedToken);

            if (!string.IsNullOrWhiteSpace(json))
            {
                using var parsedDoc = JsonDocument.Parse(json);
                aliasCache[alias] = parsedDoc.RootElement.Clone();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
        {
            // Sibling query failed and canceled the linked token for fail-fast batch abort.
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
            if (acquired) semaphore.Release();
        }
    }

    private async Task<string?> FetchOrExecuteQueryAsync(
        Guid? projectId,
        string alias,
        ResolvedDatasetContext ds,
        Dictionary<string, object> parameters,
        CancellationToken linkedToken)
    {
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
                ds.Provider, ds.ConnectionString, ds.SqlQuery, parameters, linkedToken);

            if (cacheKey is not null && datasetCache is not null && !string.IsNullOrWhiteSpace(json))
            {
                await datasetCache.SetAsync(cacheKey, json, TimeSpan.FromSeconds(ds.CacheSeconds), linkedToken);
            }
        }

        return json;
    }

    public static Dictionary<string, object> ExtractParameters(
        string query,
        IReadOnlyDictionary<string, object?> dict)
    {
        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in SqlVariablePattern().Matches(query))
        {
            var key = match.Groups[1].Value;
            if (dict.TryGetValue(key, out var val) && val is not null)
            {
                parameters[key] = ResolveParameterValue(val);
            }
        }

        return parameters;
    }

    private static object ResolveParameterValue(object value) =>
        value switch
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
            _ => value
        };
}

