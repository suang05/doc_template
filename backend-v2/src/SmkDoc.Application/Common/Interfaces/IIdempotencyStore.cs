namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Lifecycle status for an idempotent operation.
/// </summary>
public enum IdempotencyStatus
{
    InFlight,
    Completed
}

/// <summary>
/// Represents an idempotency record cached for safe replay or concurrency control.
/// </summary>
public sealed record IdempotencyRecord(
    string CacheKey,
    string RequestFingerprint,
    IdempotencyStatus Status,
    int? StatusCode,
    string? ResponseJson,
    IReadOnlyDictionary<string, string>? Headers,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Result of an atomic idempotency key acquisition attempt.
/// </summary>
public sealed record IdempotencyAcquisitionResult(
    bool IsAcquired,
    IdempotencyRecord? ExistingRecord
)
{
    public static IdempotencyAcquisitionResult Acquired() => new(true, null);

    public static IdempotencyAcquisitionResult Conflict(IdempotencyRecord record) => new(false, record);
}

/// <summary>
/// Decoupled storage abstraction for IETF Idempotency-Key management.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Atomically acquires an in-flight execution lock or returns the existing record if one is already present.
    /// </summary>
    Task<IdempotencyAcquisitionResult> TryAcquireOrGetAsync(
        string cacheKey,
        string requestFingerprint,
        TimeSpan inFlightTtl,
        CancellationToken ct = default);

    /// <summary>
    /// Saves the final successful HTTP response against the idempotency key for future replays.
    /// </summary>
    Task SaveCompletedAsync(
        string cacheKey,
        string requestFingerprint,
        int statusCode,
        string responseJson,
        IReadOnlyDictionary<string, string>? headers,
        TimeSpan completedTtl,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the idempotency key (used on failure rollback to permit client retries).
    /// </summary>
    Task RemoveAsync(string cacheKey, CancellationToken ct = default);
}
