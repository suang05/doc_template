namespace SmkDoc.Api.Contracts.IdentityAccess.ApiKeys;

public record CreateApiKeyRequest(string Name, string CallerApp, string[]? Scopes, DateTimeOffset? ExpiresAt);

public record ApiKeyResponseDto(Guid Id, string Name, string CallerApp, string Key, DateTimeOffset? ExpiresAt);
