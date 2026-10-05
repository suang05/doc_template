namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when an operation requires a usable API key but the key's validity window has elapsed.
/// </summary>
public sealed class ApiKeyExpiredException(Guid apiKeyId, DateTimeOffset expiredAt)
    : BusinessRuleViolationException($"API key '{apiKeyId}' expired at {expiredAt:O}.", "API_KEY_EXPIRED")
{
    public Guid ApiKeyId { get; } = apiKeyId;
    public DateTimeOffset ExpiredAt { get; } = expiredAt;
}
