using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Infrastructure.Security;

/// <summary>
/// Mints and deterministically hashes structured <c>smk_live_{keyId}_{secret}</c> credentials
/// backed by server-peppered HMAC-SHA256 digests.
/// </summary>
public sealed partial class ApiKeyCredentialGenerator(IOptions<ApiKeyOptions> options) : IApiKeyCredentialGenerator
{
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const string LivePrefix = "smk_live";
    private const int SecretLength = 32;

    [GeneratedRegex("^(smk_(?:live|test))_([A-Za-z0-9]{12})_([A-Za-z0-9]{16,128})$", RegexOptions.CultureInvariant)]
    private static partial Regex StructuredKeyRegex();

    public MintedApiKeyCredential MintLiveKey()
    {
        string pepper = RequireValidPepper();

        string keyId = RandomNumberGenerator.GetString(Base62Alphabet, ApiKey.StructuredKeyIdLength);
        string secret = RandomNumberGenerator.GetString(Base62Alphabet, SecretLength);
        string suffix = secret[^ApiKey.StructuredKeySuffixLength..];
        string plainTextKey = $"{LivePrefix}_{keyId}_{secret}";

        byte[] hash = ComputeSecretHash(pepper, secret);
        return new MintedApiKeyCredential(plainTextKey, LivePrefix, keyId, suffix, hash);
    }

    public bool TryParseAndHashStructuredKey(string rawApiKey, out MintedApiKeyCredential? credential)
    {
        credential = null;
        if (string.IsNullOrWhiteSpace(rawApiKey))
        {
            return false;
        }

        string trimmed = rawApiKey.Trim();
        var match = StructuredKeyRegex().Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        string pepper = RequireValidPepper();
        string keyPrefix = match.Groups[1].Value;
        string keyId = match.Groups[2].Value;
        string secret = match.Groups[3].Value;
        string suffix = secret[^ApiKey.StructuredKeySuffixLength..];

        byte[] hash = ComputeSecretHash(pepper, secret);
        credential = new MintedApiKeyCredential(trimmed, keyPrefix, keyId, suffix, hash);
        return true;
    }

    private string RequireValidPepper()
    {
        string pepper = options.Value.ApiKeyPepper;
        if (string.IsNullOrWhiteSpace(pepper) || pepper.Length < 16)
        {
            throw new InvalidOperationException("Security:ApiKeyPepper must be configured with at least 16 characters before minting API keys.");
        }

        return pepper;
    }

    private static byte[] ComputeSecretHash(string pepper, string secret)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(pepper);
        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
        return HMACSHA256.HashData(keyBytes, secretBytes);
    }
}
