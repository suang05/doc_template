using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Aggregate root representing a cryptographic API key credential for machine-to-machine access.
/// References its owning <see cref="Project"/> by Id only (no cross-aggregate navigation).
/// </summary>
public sealed class ApiKey : AggregateRoot<ApiKeyId>, IMustHaveProject
{
    public const int CallerAppMaxLength = 50;

    public const int StructuredKeyIdLength = 12;
    public const int StructuredKeySuffixLength = 4;

    public ProjectId ProjectId { get; private set; }
    public ApiKeyName Name { get; private set; } = null!;
    public string CallerApp { get; private set; } = string.Empty;
    public string KeyPrefix { get; private set; } = "smk_live";
    public string? KeyId { get; private set; }
    public string? KeySuffix { get; private set; }
    public ReadOnlyMemory<byte>? SecretHash { get; private set; }
    public bool HasSecretHash => SecretHash is { Length: 32 };
    public Sha256Hash? KeyHash { get; private set; }
    public ApiKeyScope Scope { get; private set; } = ApiKeyScope.ReadWrite;
    public ExpirationPolicy Expiration { get; private set; } = ExpirationPolicy.Never;
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    public string GetMaskedKey() =>
        !string.IsNullOrEmpty(KeyId) && !string.IsNullOrEmpty(KeySuffix)
            ? $"{KeyPrefix}_{KeyId}_****{KeySuffix}"
            : $"{KeyPrefix}_****";

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
        ApiKeyId? id,
        ProjectId projectId,
        ApiKeyName name,
        string? callerApp,
        Sha256Hash keyHash,
        ExpirationPolicy? expiration,
        DateTimeOffset now,
        ApiKeyScope? scope = null)
        : base(id.HasValue && !id.Value.IsEmpty ? id.Value : ApiKeyId.New(), createdAt: now)
    {
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        Name = Guard.NotNull(name, nameof(Name));
        KeyHash = Guard.NotNull(keyHash, nameof(KeyHash));
        KeyPrefix = "smk_legacy";
        CallerApp = Guard.MaxLength(callerApp, nameof(CallerApp), CallerAppMaxLength);
        Expiration = expiration ?? ExpirationPolicy.Never;
        Scope = scope ?? ApiKeyScope.ReadWrite;
        IsActive = true;
    }

    private ApiKey(
        ApiKeyId? id,
        ProjectId projectId,
        ApiKeyName name,
        string? callerApp,
        string keyPrefix,
        string keyId,
        string keySuffix,
        byte[] secretHashBytes,
        ExpirationPolicy? expiration,
        DateTimeOffset now,
        ApiKeyScope? scope = null)
        : base(id.HasValue && !id.Value.IsEmpty ? id.Value : ApiKeyId.New(), createdAt: now)
    {
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        Name = Guard.NotNull(name, nameof(Name));
        KeyPrefix = Guard.MaxLength(Guard.NotBlank(keyPrefix, nameof(KeyPrefix)), nameof(KeyPrefix), 16);
        KeyId = Guard.NotBlank(keyId, nameof(KeyId));
        if (KeyId.Length != StructuredKeyIdLength)
        {
            throw new DomainValidationException($"KeyId must be exactly {StructuredKeyIdLength} characters.");
        }

        KeySuffix = Guard.NotBlank(keySuffix, nameof(KeySuffix));
        if (KeySuffix.Length != StructuredKeySuffixLength)
        {
            throw new DomainValidationException($"KeySuffix must be exactly {StructuredKeySuffixLength} characters.");
        }

        Guard.NotNull(secretHashBytes, nameof(secretHashBytes));
        if (secretHashBytes.Length != 32)
        {
            throw new DomainValidationException("SecretHash must be 32 bytes (HMAC-SHA256).");
        }

        SecretHash = secretHashBytes.ToArray();
        CallerApp = Guard.MaxLength(callerApp, nameof(CallerApp), CallerAppMaxLength);
        Expiration = expiration ?? ExpirationPolicy.Never;
        Scope = scope ?? ApiKeyScope.ReadWrite;
        IsActive = true;
    }

    /// <summary>
    /// Factory: creates a legacy SHA-256 backed API key (preserved during Phase 4 transition / unit tests).
    /// </summary>
    public static ApiKey Issue(ProjectId projectId, ApiKeyName name, string? callerApp, Sha256Hash keyHash,
        ExpirationPolicy expiration, DateTimeOffset now, ApiKeyScope? scope = null) =>
        new(null, projectId, name, callerApp, keyHash, expiration ?? ExpirationPolicy.Never, now, scope);

    /// <summary>
    /// Canonical factory for structured <c>smk_live_{keyId}_{secret}</c> HMAC-SHA256 backed API keys.
    /// </summary>
    public static ApiKey IssueStructured(
        ProjectId projectId,
        ApiKeyName name,
        string? callerApp,
        string keyPrefix,
        string keyId,
        string keySuffix,
        byte[] secretHashBytes,
        ExpirationPolicy? expiration,
        DateTimeOffset now,
        ApiKeyScope? scope = null) =>
        new(null, projectId, name, callerApp, keyPrefix, keyId, keySuffix, secretHashBytes, expiration ?? ExpirationPolicy.Never, now, scope);

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
            throw new ApiKeyRevokedException(Id.Value);
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
        AddDomainEvent(new ApiKeyRevokedDomainEvent(Id, ProjectId, now));
    }

    private void EnsureUsableAt(DateTimeOffset now)
    {
        if (IsRevoked)
        {
            throw new ApiKeyRevokedException(Id.Value);
        }

        if (IsExpiredAt(now))
        {
            throw new ApiKeyExpiredException(Id.Value, Expiration.ExpiresAt!.Value);
        }
    }
}
