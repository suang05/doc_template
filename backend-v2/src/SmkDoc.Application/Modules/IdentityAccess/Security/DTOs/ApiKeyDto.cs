namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// DTO representing an API Key record. MaskedKey exposes only the prefix and trailing hash characters.
/// </summary>
public sealed record ApiKeyDto(
    Guid Id,
    string Name,
    string CallerApp,
    bool IsActive,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    string Scope = "ReadWrite",
    string MaskedKey = ""
);

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
/// DTO representing an API key provisioned for a workspace.
/// </summary>
public sealed record ProvisionedApiKeyDto(
    string Name,
    string Scope,
    string PlainTextKey
);

/// <summary>
/// DTO returned after validating an API key.
/// </summary>
public sealed record ValidatedApiKeyDto(
    Guid Id,
    string CallerApp,
    Guid ProjectId,
    string Scope
);
