namespace SmkDoc.Api.Contracts.IdentityAccess.ApiKeys;

public sealed record CreateApiKeyRequest(
    string Name,
    string CallerApp,
    string Scope = "ReadWrite",
    IReadOnlyList<string>? Scopes = null,
    DateTimeOffset? ExpiresAt = null)
{
    public string EffectiveScope => ResolveEffectiveScope();

    private string ResolveEffectiveScope()
    {
        if (!string.IsNullOrWhiteSpace(Scope))
            return Scope;

        return Scopes is { Count: > 0 } ? Scopes[0] : "ReadWrite";
    }
}
