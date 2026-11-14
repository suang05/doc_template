using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Security;

/// <summary>
/// Encrypts and decrypts sensitive configuration payloads (e.g. data connection strings)
/// using AES-256-GCM authenticated encryption (<c>v2:{nonce}:{cipher}:{tag}</c>),
/// with backward-compatible decryption for legacy <c>v1</c> AES-CBC values.
/// </summary>
public sealed class DataProtectionService : IDataProtectionService
{
    private const int KeyByteLength = 32;
    private const int NonceByteLength = 12;
    private const int TagByteLength = 16;
    private const int LegacyV1IvLength = 16;
    private const string V2Prefix = "v2";
    private static readonly byte[] V2AssociatedData = "smkdoc:v2"u8.ToArray();

    private readonly byte[] _v2Key;
    private readonly byte[] _legacyV1Key;

    public DataProtectionService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredKey = configuration["EncryptionKey"];
        if (string.IsNullOrWhiteSpace(configuredKey) ||
            Encoding.UTF8.GetByteCount(configuredKey) < KeyByteLength)
        {
            throw new InvalidOperationException(
                "EncryptionKey is required and must be at least 32 bytes (256 bits). Set EncryptionKey in configuration or environment variables.");
        }

        _v2Key = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        _legacyV1Key = Encoding.UTF8.GetBytes(configuredKey.PadRight(KeyByteLength)[..KeyByteLength]);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceByteLength);
        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[TagByteLength];

        try
        {
            using var aesGcm = new AesGcm(_v2Key, TagByteLength);
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag, V2AssociatedData);
            return $"{V2Prefix}:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(cipherBytes)}:{Convert.ToBase64String(tag)}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        if (cipherText.StartsWith($"{V2Prefix}:", StringComparison.Ordinal))
        {
            return DecryptV2(cipherText);
        }

        return DecryptLegacyV1Cbc(cipherText);
    }

    private string DecryptV2(string cipherText)
    {
        var parts = cipherText.Split(':');
        if (parts.Length != 4 || !string.Equals(parts[0], V2Prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException("Invalid v2 ciphertext envelope.");
        }

        byte[] nonce;
        byte[] cipherBytes;
        byte[] tag;
        try
        {
            nonce = Convert.FromBase64String(parts[1]);
            cipherBytes = Convert.FromBase64String(parts[2]);
            tag = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Invalid base64 encoding in v2 ciphertext envelope.", ex);
        }

        if (nonce.Length != NonceByteLength || tag.Length != TagByteLength)
        {
            throw new CryptographicException("Invalid nonce or authentication tag length in v2 ciphertext envelope.");
        }

        byte[] plainBytes = new byte[cipherBytes.Length];
        try
        {
            using var aesGcm = new AesGcm(_v2Key, TagByteLength);
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes, V2AssociatedData);
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    private string DecryptLegacyV1Cbc(string cipherText)
    {
        byte[] cipherBytes = Convert.FromBase64String(cipherText);
        Span<byte> legacyZeroIv = stackalloc byte[LegacyV1IvLength];
        legacyZeroIv.Clear();

        using var aes = Aes.Create();
        aes.Key = _legacyV1Key;
        byte[] plainBytes = aes.DecryptCbc(cipherBytes, legacyZeroIv, PaddingMode.PKCS7);
        try
        {
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }
}
