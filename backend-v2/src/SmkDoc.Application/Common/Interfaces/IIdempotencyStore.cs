namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Lifecycle status for a durable idempotency record.
/// </summary>
public enum IdempotencyStatus
{
    InFlight,
    Completed,
    Failed
}

/// <summary>
/// Represents a durable idempotency record stored for safe replay or concurrency control.
/// </summary>
public sealed record IdempotencyRecord(
    string CacheKey,
    string RequestFingerprint,
    IdempotencyStatus Status,
    int? StatusCode,
    string? ResponseJson,
    IReadOnlyDictionary<string, string>? Headers,
    DateTimeOffset CreatedAt,
    Guid? ResultResourceId = null,
    DateTimeOffset? LockedUntil = null
);

/// <summary>
/// Structured input for atomic idempotency key acquisition.
/// </summary>
public sealed record IdempotencyAcquisitionRequest(
    Guid? ProjectId,
    string CredentialId,
    string Scope,
    string IdempotencyKey,
    string RequestFingerprint,
    TimeSpan InFlightTtl,
    TimeSpan CompletedTtl);

/// <summary>
/// Structured input for persisting the completed outcome of an idempotent operation.
/// </summary>
public sealed record IdempotencyCompletionRequest(
    Guid? ProjectId,
    string CredentialId,
    string Scope,
    string IdempotencyKey,
    string RequestFingerprint,
    int StatusCode,
    string ResponseJson,
    IReadOnlyDictionary<string, string>? Headers,
    TimeSpan CompletedTtl,
    Guid? ResultResourceId = null);

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
/// Decoupled durable storage abstraction for IETF Idempotency-Key management.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Atomically acquires an in-flight execution lock or returns the existing record if one is active or completed.
    /// </summary>
    Task<IdempotencyAcquisitionResult> TryAcquireOrGetAsync(
        IdempotencyAcquisitionRequest request,
        CancellationToken ct);

    /// <summary>
    /// Saves the final successful response against the durable idempotency key for future replays.
    /// </summary>
    Task SaveCompletedAsync(
        IdempotencyCompletionRequest request,
        CancellationToken ct);

    /// <summary>
    /// Releases an in-flight idempotency key on unhandled failure so clients can safely retry.
    /// </summary>
    Task RemoveAsync(
        Guid? projectId,
        string credentialId,
        string scope,
        string idempotencyKey,
        CancellationToken ct);
}
