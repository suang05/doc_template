using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmkDoc.Infrastructure.Security;

namespace SmkDoc.Tests.Infrastructure.Security;

public sealed class DataProtectionServiceTests
{
    private const string ValidTestKey = "my_super_secret_test_key_32_bytes!";
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    public DataProtectionServiceTests()
    {
        _config["EncryptionKey"].Returns(ValidTestKey);
    }

    private DataProtectionService CreateSut() => new(_config);

    [Fact]
    public void EncryptAndDecrypt_V2Aead_ShouldRestoreOriginalPlainTextWithRandomNonces()
    {
        var sut = CreateSut();
        var plainText = "Host=prod-db.internal;Port=5432;Database=smkdoc;Password=SuperSecretP@ssw0rd;";

        var encrypted1 = sut.Encrypt(plainText);
        var encrypted2 = sut.Encrypt(plainText);

        encrypted1.Should().StartWith("v2:");
        encrypted1.Split(':').Should().HaveCount(4);
        encrypted1.Should().NotBe(encrypted2);

        sut.Decrypt(encrypted1).Should().Be(plainText);
        sut.Decrypt(encrypted2).Should().Be(plainText);
    }

    [Fact]
    public void Decrypt_WhenV2CiphertextOrTagTampered_ShouldThrowCryptographicException()
    {
        var sut = CreateSut();
        var encrypted = sut.Encrypt("SensitiveConnectionString=12345;");
        var parts = encrypted.Split(':');

        var cipherBytes = Convert.FromBase64String(parts[2]);
        cipherBytes[0] ^= 0xFF;
        var tamperedCipher = $"{parts[0]}:{parts[1]}:{Convert.ToBase64String(cipherBytes)}:{parts[3]}";

        var tagBytes = Convert.FromBase64String(parts[3]);
        tagBytes[0] ^= 0xFF;
        var tamperedTag = $"{parts[0]}:{parts[1]}:{parts[2]}:{Convert.ToBase64String(tagBytes)}";

        var actCipher = () => sut.Decrypt(tamperedCipher);
        var actTag = () => sut.Decrypt(tamperedTag);

        actCipher.Should().Throw<CryptographicException>();
        actTag.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_WithDifferentKey_ShouldThrowCryptographicException()
    {
        var sut1 = CreateSut();
        var cipherText = sut1.Encrypt("SecretPayload");

        var otherConfig = Substitute.For<IConfiguration>();
        otherConfig["EncryptionKey"].Returns("another_super_secret_test_key_32_bytes!");
        var sut2 = new DataProtectionService(otherConfig);

        var act = () => sut2.Decrypt(cipherText);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_LegacyV1CbcPayload_ShouldRemainCompatible()
    {
        var sut = CreateSut();
        const string plainText = "Host=legacy-db;Database=smkdoc;Password=LegacySecret;";

        byte[] legacyKey = Encoding.UTF8.GetBytes(ValidTestKey.PadRight(32)[..32]);
        byte[] legacyZeroIv = new byte[16];
        using var aes = Aes.Create();
        aes.Key = legacyKey;
        string legacyV1Base64 = Convert.ToBase64String(
            aes.EncryptCbc(Encoding.UTF8.GetBytes(plainText), legacyZeroIv, PaddingMode.PKCS7));

        legacyV1Base64.Should().NotStartWith("v2:");
        sut.Decrypt(legacyV1Base64).Should().Be(plainText);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Encrypt_EmptyOrNull_ShouldReturnInputAsIs(string? input)
    {
        var sut = CreateSut();

        var result = sut.Encrypt(input!);
        result.Should().Be(input);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Decrypt_EmptyOrNull_ShouldReturnInputAsIs(string? input)
    {
        var sut = CreateSut();

        var result = sut.Decrypt(input!);
        result.Should().Be(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short-key-under-32-bytes")]
    public void Constructor_WhenEncryptionKeyMissingOrTooShort_ShouldThrowInvalidOperationException(string? invalidKey)
    {
        var badConfig = Substitute.For<IConfiguration>();
        badConfig["EncryptionKey"].Returns(invalidKey);

        var act = () => new DataProtectionService(badConfig);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*EncryptionKey*32 bytes*");
    }
}
