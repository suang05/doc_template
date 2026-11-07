namespace SmkDoc.Api.Contracts.IdentityAccess.ApiKeys;

public sealed record CreateApiKeyRequest(
    string Name,
    string CallerApp,
    string Scope = "ReadWrite",
    string[]? Scopes = null,
    DateTimeOffset? ExpiresAt = null)
{
    public string EffectiveScope => !string.IsNullOrWhiteSpace(Scope)
        ? Scope
        : (Scopes?.FirstOrDefault() ?? "ReadWrite");
}
