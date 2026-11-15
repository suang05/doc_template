using System.Text.Json.Serialization;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Result DTO representing an API Key record. MaskedKey exposes only the key prefix and trailing characters.
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
    [property: JsonIgnore] long Version = 1
) : IHasVersion, IHasResourceId
{
    [JsonIgnore]
    public Guid ResourceId => Id;
}

/// <summary>
/// Result returned immediately after creating an API key, containing the one-time plain-text key.
/// </summary>
public sealed record CreateApiKeyResultDto(
    Guid Id,
    string Name,
    string CallerApp,
    string PlainTextKey,
    string Scope = "ReadWrite",
    DateTimeOffset? ExpiresAt = null,
    [property: JsonIgnore] long Version = 1
) : IHasVersion, IHasResourceId
{
    public string Key => PlainTextKey;

    [JsonIgnore]
    public Guid ResourceId => Id;
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

