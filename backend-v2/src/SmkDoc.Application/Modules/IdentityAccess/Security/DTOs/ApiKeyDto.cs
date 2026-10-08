namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// DTO representing an API Key record.
/// </summary>
public record ApiKeyDto(
    Guid Id,
    string Name,
    string CallerApp,
    bool IsActive,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    string Scope = "ReadWrite"
);

/// <summary>
/// Result returned immediately after creating an API key, containing the one-time plain-text key.
/// </summary>
public record CreateApiKeyResultDto(
    Guid Id,
    string Name,
    string CallerApp,
    string PlainTextKey
);

/// <summary>
/// DTO representing an API key provisioned for a workspace.
/// </summary>
public record ProvisionedApiKeyDto(
    string Name,
    string Scope,
    string PlainTextKey
);

/// <summary>
/// DTO returned after validating an API key.
/// </summary>
public record ValidatedApiKeyDto(
    Guid Id,
    string CallerApp,
    Guid ProjectId,
    string Scope
);
