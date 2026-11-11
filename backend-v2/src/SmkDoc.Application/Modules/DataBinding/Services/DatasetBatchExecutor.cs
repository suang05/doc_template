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
        if (datasetAliases.Count == 0) return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        var aliasCache = new ConcurrentDictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        using var semaphore = new SemaphoreSlim(MaxConcurrentDatasetQueries, MaxConcurrentDatasetQueries);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var tasks = datasetAliases.Select(kvp =>
            ExecuteSingleQueryWithGuardAsync(kvp.Key, kvp.Value, callerContext, projectId, semaphore, linkedCts, aliasCache, ct));

        await Task.WhenAll(tasks);
        return aliasCache;
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
        catch (OperationCanceledException) when (linkedToken.IsCancellationRequested) { }
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

