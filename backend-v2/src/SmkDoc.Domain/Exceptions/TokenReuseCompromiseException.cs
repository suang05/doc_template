using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Domain exception triggered when an invalidated or already-rotated refresh token
/// is presented outside the allowable leeway window, indicating potential session hijack.
/// </summary>
public sealed class TokenReuseCompromiseException(UserId userId)
    : DomainException("Invalid refresh token (reuse detected).", "TOKEN_REUSE_DETECTED")
{
    public UserId UserId { get; } = userId;
}

