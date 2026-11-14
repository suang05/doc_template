namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Result DTO representing an API Key record. MaskedKey exposes only the prefix and trailing hash characters.
/// </summary>
public sealed record ApiKeyResultDto(
    Guid Id,
    string Name,
    string CallerApp,
    bool IsActive,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    string Scope = "ReadWrite",
    string MaskedKey = "",
    string? ConcurrencyToken = null
) : IHasConcurrencyToken;

/// <summary>
/// Result returned immediately after creating an API key, containing the one-time plain-text key.
/// </summary>
public sealed record CreateApiKeyResultDto(
    Guid Id,
    string Name,
    string CallerApp,
    string PlainTextKey,
    string Scope = "ReadWrite",
    DateTimeOffset? ExpiresAt = null
)
{
    public string Key => PlainTextKey;
}

/// <summary>
/// Result DTO returned after validating an API key.
/// </summary>
public sealed record ValidatedApiKeyResultDto(
    Guid Id,
    string CallerApp,
    Guid ProjectId,
    string Scope
);

