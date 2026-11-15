using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Infrastructure.Security;

/// <summary>
/// Mints structured <c>smk_live_{keyId}_{secret}</c> credentials backed by server-peppered HMAC-SHA256 digests.
/// </summary>
public sealed class ApiKeyCredentialGenerator(IOptions<ApiKeyOptions> options) : IApiKeyCredentialGenerator
{
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const string LivePrefix = "smk_live";
    private const int SecretLength = 32;

    public MintedApiKeyCredential MintLiveKey()
    {
        string pepper = options.Value.ApiKeyPepper;
        if (string.IsNullOrWhiteSpace(pepper) || pepper.Length < 16)
        {
            throw new InvalidOperationException("Security:ApiKeyPepper must be configured with at least 16 characters before minting API keys.");
        }

        string keyId = RandomNumberGenerator.GetString(Base62Alphabet, ApiKey.StructuredKeyIdLength);
        string secret = RandomNumberGenerator.GetString(Base62Alphabet, SecretLength);
        string suffix = secret[^ApiKey.StructuredKeySuffixLength..];
        string plainTextKey = $"{LivePrefix}_{keyId}_{secret}";

        byte[] keyBytes = Encoding.UTF8.GetBytes(pepper);
        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
        byte[] hash = HMACSHA256.HashData(keyBytes, secretBytes);

        return new MintedApiKeyCredential(plainTextKey, LivePrefix, keyId, suffix, hash);
    }
}
