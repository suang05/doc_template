namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when an operation requires a usable API key but the key has been revoked.
/// </summary>
public sealed class ApiKeyRevokedException(Guid apiKeyId)
    : BusinessRuleViolationException($"API key '{apiKeyId}' has been revoked.", "API_KEY_REVOKED")
{
    public Guid ApiKeyId { get; } = apiKeyId;
}
