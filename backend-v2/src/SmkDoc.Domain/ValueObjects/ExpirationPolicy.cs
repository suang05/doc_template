using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Validity window for a credential or policy; encapsulates expiration invariants.
/// </summary>
public sealed class ExpirationPolicy : ValueObject
{
    public DateTimeOffset? ExpiresAt { get; }

    private ExpirationPolicy(DateTimeOffset? expiresAt)
    {
        ExpiresAt = expiresAt;
    }

    public static ExpirationPolicy Never { get; } = new((DateTimeOffset?)null);

    public static ExpirationPolicy Until(DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (expiresAt <= now)
        {
            throw new DomainValidationException("API key expiration must be in the future.", "API_KEY_EXPIRY_IN_PAST");
        }

        return new(expiresAt);
    }

    /// <summary>
    /// Materializes an existing expiration timestamp (e.g. from ORM persistence) without re-validating that it is in the future.
    /// </summary>
    public static ExpirationPolicy FromExisting(DateTimeOffset? expiresAt) => new(expiresAt);

    public bool IsExpiredAt(DateTimeOffset now) => ExpiresAt.HasValue && ExpiresAt.Value <= now;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ExpiresAt;
    }

    public override string ToString() =>
        ExpiresAt.HasValue ? ExpiresAt.Value.ToString("O") : "Never";
}
