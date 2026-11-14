using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Cache;

/// <summary>
/// In-memory implementation of <see cref="IIdempotencyStore"/> using <see cref="IMemoryCache"/>.
/// Thread-safe for atomic in-flight locking and response caching in single-node environments.
/// </summary>
public sealed class MemoryIdempotencyStore(
    IMemoryCache memoryCache,
    TimeProvider timeProvider) : IIdempotencyStore
{
    private readonly Lock _syncLock = new();

    public Task<IdempotencyAcquisitionResult> TryAcquireOrGetAsync(
        string cacheKey,
        string requestFingerprint,
        TimeSpan inFlightTtl,
        CancellationToken ct)
    {
        lock (_syncLock)
        {
            if (memoryCache.TryGetValue(cacheKey, out IdempotencyRecord? existing) && existing is not null)
            {
                var conflict = IdempotencyAcquisitionResult.Conflict(existing);
                return Task.FromResult(conflict);
            }

            var now = timeProvider.GetUtcNow();
            var inFlightRecord = new IdempotencyRecord(
                cacheKey,
                requestFingerprint,
                IdempotencyStatus.InFlight,
                null,
                null,
                null,
                now);

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
        CancellationToken ct)
    {
        lock (_syncLock)
        {
            var now = timeProvider.GetUtcNow();
            var completedRecord = new IdempotencyRecord(
                cacheKey,
                requestFingerprint,
                IdempotencyStatus.Completed,
                statusCode,
                responseJson,
                headers,
                now);

            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = completedTtl
            };

            memoryCache.Set(cacheKey, completedRecord, entryOptions);
            return Task.CompletedTask;
        }
    }

    public Task RemoveAsync(string cacheKey, CancellationToken ct)
    {
        lock (_syncLock)
        {
            if (memoryCache.TryGetValue(cacheKey, out IdempotencyRecord? existing) &&
                existing?.Status == IdempotencyStatus.Completed)
            {
                return Task.CompletedTask;
            }

            memoryCache.Remove(cacheKey);
            return Task.CompletedTask;
        }
    }
}
