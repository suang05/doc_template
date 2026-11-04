namespace SmkDoc.Api.Contracts.IdentityAccess.ApiKeys;

public record CreateApiKeyRequest(
    string Name,
    string CallerApp,
    string Scope = "ReadWrite",
    string[]? Scopes = null,
    DateTimeOffset? ExpiresAt = null,
    Guid? ProjectId = null
);

public record ApiKeyResponseDto(
    Guid Id,
    string Name,
    string CallerApp,
    string Key,
    string Scope = "ReadWrite",
    DateTimeOffset? ExpiresAt = null);
