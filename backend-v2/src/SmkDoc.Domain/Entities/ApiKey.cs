using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Aggregate root representing a cryptographic API key credential for machine-to-machine access.
/// References its owning <see cref="Project"/> by Id only (no cross-aggregate navigation).
/// </summary>
public sealed class ApiKey : BaseEntity, IMustHaveProject
{
    public const int CallerAppMaxLength = 50;

    public Guid ProjectId { get; private set; }
    public ApiKeyName Name { get; private set; } = null!;
    public string CallerApp { get; private set; } = string.Empty;
    public Sha256Hash KeyHash { get; private set; } = null!;
    public ApiKeyScope Scope { get; private set; } = ApiKeyScope.ReadWrite;
    public ExpirationPolicy Expiration { get; private set; } = ExpirationPolicy.Never;
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>
    /// Read-only projection of Expiration.ExpiresAt for consumer convenience.
    /// </summary>
    public DateTimeOffset? ExpiresAt => Expiration?.ExpiresAt;

    public bool IsRevoked => !IsActive;

    // For ORM materialization only
    private ApiKey()
    {
    }

    internal ApiKey(
        Guid? id,
        Guid projectId,
        ApiKeyName name,
        string? callerApp,
        Sha256Hash keyHash,
        ExpirationPolicy? expiration,
        DateTimeOffset now,
        ApiKeyScope? scope = null)
        : base(id, createdAt: now)
    {
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        Name = Guard.NotNull(name, nameof(Name));
        KeyHash = Guard.NotNull(keyHash, nameof(KeyHash));
        CallerApp = Guard.MaxLength(callerApp, nameof(CallerApp), CallerAppMaxLength);
        Expiration = expiration ?? ExpirationPolicy.Never;
        Scope = scope ?? ApiKeyScope.ReadWrite;
        IsActive = true;
    }

    /// <summary>
    /// Factory: the only way to create a valid API key with an explicit ExpirationPolicy.
    /// </summary>
    public static ApiKey Issue(Guid projectId, ApiKeyName name, string? callerApp, Sha256Hash keyHash,
        ExpirationPolicy expiration, DateTimeOffset now, ApiKeyScope? scope = null) =>
        new(null, projectId, name, callerApp, keyHash, expiration ?? ExpirationPolicy.Never, now, scope);

    public bool IsExpiredAt(DateTimeOffset now) => Expiration.IsExpiredAt(now);

    public bool IsUsableAt(DateTimeOffset now) => IsActive && !IsExpiredAt(now);

    /// <summary>
    /// Records an authenticated M2M call. Fails fast if the key is revoked or expired.
    /// </summary>
    public void RecordUsage(DateTimeOffset now)
    {
        EnsureUsableAt(now);
        LastUsedAt = now;
        SetUpdated(now);
    }

    public void Rename(ApiKeyName newName, DateTimeOffset now)
    {
        Guard.NotNull(newName, nameof(newName));
        if (IsRevoked)
        {
            throw new ApiKeyRevokedException(Id);
        }

        if (Name == newName)
        {
            return;
        }

        Name = newName;
        SetUpdated(now);
    }

    /// <summary>
    /// Idempotent: revoking an already revoked key is a no-op (safe for retries).
    /// </summary>
    public void Revoke(DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return;
        }

        IsActive = false;
        SetUpdated(now);
    }

    private void EnsureUsableAt(DateTimeOffset now)
    {
        if (IsRevoked)
        {
            throw new ApiKeyRevokedException(Id);
        }

        if (IsExpiredAt(now))
        {
            throw new ApiKeyExpiredException(Id, Expiration.ExpiresAt!.Value);
        }
    }
}
