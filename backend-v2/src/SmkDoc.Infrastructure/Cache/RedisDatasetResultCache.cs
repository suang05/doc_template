using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Cache;

/// <summary>
/// Multi-pod distributed implementation of <see cref="IDatasetResultCache"/> backed by Redis.
/// Enforces fail-open semantics: transient Redis errors log a warning and fall back to live SQL execution.
/// </summary>
public sealed partial class RedisDatasetResultCache(
    IConnectionMultiplexer multiplexer,
    ILogger<RedisDatasetResultCache> logger,
    string instancePrefix = RedisConfigurationResolver.DefaultInstancePrefix) : IDatasetResultCache
{
    public async ValueTask<string?> GetAsync(string cacheKey, CancellationToken ct)
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
            LogCacheReadFailed(logger, ex, cacheKey);
            return null;
        }
    }

    public async ValueTask SetAsync(string cacheKey, string jsonResult, TimeSpan ttl, CancellationToken ct)
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
            LogCacheWriteFailed(logger, ex, cacheKey);
        }
    }

    private RedisKey PrefixedKey(string cacheKey) => $"{instancePrefix}{cacheKey}";

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Redis dataset cache read failed for {CacheKey}; failing open to live dataset query.")]
    private static partial void LogCacheReadFailed(ILogger logger, RedisException ex, string cacheKey);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Redis dataset cache write failed for {CacheKey}; continuing without caching.")]
    private static partial void LogCacheWriteFailed(ILogger logger, RedisException ex, string cacheKey);
}
