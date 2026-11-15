using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

public sealed record VerifiedApiKeyResult(
    ApiKeyId KeyId,
    ProjectId ProjectId,
    ApiKeyScope Scope,
    string CallerApp,
    bool IsLegacySha256);

/// <summary>
/// Constant-time API key credential verifier supporting <c>smk_live_{keyId}_{secret}</c> (HMAC-SHA256)
/// and transitional legacy SHA-256 verification.
/// </summary>
public interface IApiKeyVerifier
{
    Task<VerifiedApiKeyResult?> VerifyAsync(string rawApiKey, CancellationToken ct);
    Task TouchUsageAsync(ApiKeyId apiKeyId, DateTimeOffset now, CancellationToken ct);
}
