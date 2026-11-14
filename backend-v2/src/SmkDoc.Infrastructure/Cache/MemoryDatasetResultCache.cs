using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Cache;

public sealed class MemoryDatasetResultCache(IMemoryCache cache) : IDatasetResultCache
{
    public ValueTask<string?> GetAsync(string cacheKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ct.ThrowIfCancellationRequested();

        cache.TryGetValue(cacheKey, out string? cachedJson);
        return ValueTask.FromResult(cachedJson);
    }

    public ValueTask SetAsync(string cacheKey, string jsonResult, TimeSpan ttl, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ct.ThrowIfCancellationRequested();

        if (ttl <= TimeSpan.Zero || string.IsNullOrWhiteSpace(jsonResult))
        {
            return ValueTask.CompletedTask;
        }

        cache.Set(cacheKey, jsonResult, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        });

        return ValueTask.CompletedTask;
    }
}
