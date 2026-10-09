using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Cache;

/// <summary>
/// In-memory implementation of <see cref="IIdempotencyStore"/> using <see cref="IMemoryCache"/>.
/// Thread-safe for atomic in-flight locking and response caching in single-node environments.
/// </summary>
public sealed class MemoryIdempotencyStore(IMemoryCache memoryCache) : IIdempotencyStore
{
    private readonly object syncLock = new();

    public Task<IdempotencyAcquisitionResult> TryAcquireOrGetAsync(
        string cacheKey,
        string requestFingerprint,
        TimeSpan inFlightTtl,
        CancellationToken ct = default)
    {
        lock (syncLock)
        {
            if (memoryCache.TryGetValue(cacheKey, out IdempotencyRecord? existing) && existing is not null)
            {
                var conflict = IdempotencyAcquisitionResult.Conflict(existing);
                return Task.FromResult(conflict);
            }

            var inFlightRecord = new IdempotencyRecord(
                cacheKey,
                requestFingerprint,
                IdempotencyStatus.InFlight,
                null,
                null,
                null,
                DateTimeOffset.UtcNow);

            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = inFlightTtl
            };

            memoryCache.Set(cacheKey, inFlightRecord, entryOptions);
            var acquired = IdempotencyAcquisitionResult.Acquired();
            return Task.FromResult(acquired);
        }
    }

    public Task SaveCompletedAsync(
        string cacheKey,
        string requestFingerprint,
        int statusCode,
        string responseJson,
        IReadOnlyDictionary<string, string>? headers,
        TimeSpan completedTtl,
        CancellationToken ct = default)
    {
        lock (syncLock)
        {
            var completedRecord = new IdempotencyRecord(
                cacheKey,
                requestFingerprint,
                IdempotencyStatus.Completed,
                statusCode,
                responseJson,
                headers,
                DateTimeOffset.UtcNow);

            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = completedTtl
            };

            memoryCache.Set(cacheKey, completedRecord, entryOptions);
            return Task.CompletedTask;
        }
    }

    public Task RemoveAsync(string cacheKey, CancellationToken ct = default)
    {
        lock (syncLock)
        {
            memoryCache.Remove(cacheKey);
            return Task.CompletedTask;
        }
    }
}
