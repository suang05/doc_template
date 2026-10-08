using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Entity representing a persistent refresh token credential for web portal session management.
/// Uses SHA-256 hashed storage to ensure plain tokens are never stored in the database.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public Sha256Hash TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Sha256Hash? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked => RevokedAt.HasValue;

    // For ORM materialization only
    private RefreshToken()
    {
    }

    internal RefreshToken(
        Guid? id,
        Guid userId,
        Sha256Hash tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        DateTimeOffset? revokedAt = null,
        Sha256Hash? replacedByTokenHash = null)
        : base(id, createdAt: now)
    {
        UserId = Guard.NotEmpty(userId, nameof(UserId));
        TokenHash = Guard.NotNull(tokenHash, nameof(TokenHash));
        
        if (expiresAt <= now)
        {
            throw new DomainValidationException("RefreshToken expiration must be strictly in the future.");
        }

        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
        ReplacedByTokenHash = replacedByTokenHash;
    }

    /// <summary>
    /// Canonical factory method: creates a new active RefreshToken.
    /// </summary>
    public static RefreshToken Create(
        Guid userId,
        Sha256Hash tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset now) =>
        new(null, userId, tokenHash, expiresAt, now);

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    /// <summary>
    /// Revokes this token with deterministic time, optionally linking to the replacement token hash (Rotation).
    /// </summary>
    public void Revoke(DateTimeOffset now, Sha256Hash? replacedBy = null)
    {
        if (IsRevoked)
        {
            throw new BusinessRuleViolationException("RefreshToken has already been revoked.");
        }

        RevokedAt = now;
        ReplacedByTokenHash = replacedBy;
        SetUpdated(now);
    }
}
