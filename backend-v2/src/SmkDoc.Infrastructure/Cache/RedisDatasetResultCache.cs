using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Cache;

/// <summary>
/// Multi-pod distributed implementation of <see cref="IDatasetResultCache"/> backed by Redis.
/// Enforces fail-open semantics: transient Redis errors log a warning and fall back to live SQL execution.
/// </summary>
public sealed class RedisDatasetResultCache(
    IConnectionMultiplexer multiplexer,
    ILogger<RedisDatasetResultCache> logger,
    string instancePrefix = RedisConfigurationResolver.DefaultInstancePrefix) : IDatasetResultCache
{
    public async ValueTask<string?> GetAsync(string cacheKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ct.ThrowIfCancellationRequested();

        try
        {
            var value = await multiplexer.GetDatabase().StringGetAsync(PrefixedKey(cacheKey));
            return value.HasValue ? (string?)value : null;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(
                ex,
                "Redis dataset cache read failed for {CacheKey}; failing open to live dataset query.",
                cacheKey);
            return null;
        }
    }

    public async ValueTask SetAsync(string cacheKey, string jsonResult, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ct.ThrowIfCancellationRequested();

        if (ttl <= TimeSpan.Zero || string.IsNullOrWhiteSpace(jsonResult))
        {
            return;
        }

        try
        {
            await multiplexer.GetDatabase().StringSetAsync(PrefixedKey(cacheKey), jsonResult, ttl);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(
                ex,
                "Redis dataset cache write failed for {CacheKey}; continuing without caching.",
                cacheKey);
        }
    }

    private RedisKey PrefixedKey(string cacheKey) => $"{instancePrefix}{cacheKey}";
}
