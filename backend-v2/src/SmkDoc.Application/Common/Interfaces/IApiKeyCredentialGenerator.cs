namespace SmkDoc.Application.Common.Interfaces;

public sealed record MintedApiKeyCredential(
    string PlainTextKey,
    string KeyPrefix,
    string KeyId,
    string KeySuffix,
    ReadOnlyMemory<byte> SecretHash);

/// <summary>
/// Generates structured <c>smk_live_{keyId}_{secret}</c> API key credentials with HMAC-SHA256 hashing.
/// </summary>
public interface IApiKeyCredentialGenerator
{
    MintedApiKeyCredential MintLiveKey();
    bool TryParseAndHashStructuredKey(string rawApiKey, out MintedApiKeyCredential? credential);
}
