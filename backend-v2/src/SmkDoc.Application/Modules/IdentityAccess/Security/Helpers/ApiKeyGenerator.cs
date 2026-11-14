using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;

public static class ApiKeyGenerator
{
    public const int DefaultSecretByteLength = 32;

    /// <summary>
    /// Generates a cryptographically strong API key secret prefixed with <paramref name="prefix"/>
    /// using <see cref="RandomNumberGenerator.GetBytes(int)"/> and Base64Url encoding (256-bit default entropy).
    /// </summary>
    public static string GenerateSecret(string prefix, int randomByteLength = DefaultSecretByteLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentOutOfRangeException.ThrowIfLessThan(randomByteLength, 16);

        byte[] randomBytes = RandomNumberGenerator.GetBytes(randomByteLength);
        return $"{prefix}_{Base64Url.EncodeToString(randomBytes)}";
    }

    public static string ComputeHash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}

