using Microsoft.Extensions.Configuration;
using SmkDoc.Infrastructure.Security;

namespace SmkDoc.Tests.Infrastructure.Security;

public sealed class DataProtectionServiceTests
{
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    private DataProtectionService CreateSut() => new(_config);

    [Fact]
    public void EncryptAndDecrypt_ShouldRestoreOriginalPlainText()
    {
        _config["EncryptionKey"].Returns("my_super_secret_test_key_32_bytes!");

        var sut = CreateSut();
        var plainText = "Host=prod-db.internal;Port=5432;Database=smkdoc;Password=SuperSecretP@ssw0rd;";

        var encrypted = sut.Encrypt(plainText);
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotBe(plainText);

        var decrypted = sut.Decrypt(encrypted);
        decrypted.Should().Be(plainText);
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

    [Fact]
    public void DefaultKey_ShouldWorkWhenConfigurationKeyIsMissing()
    {
        _config["EncryptionKey"].Returns((string?)null);

        var sut = CreateSut();
        var plainText = "Sample sensitive text";

        var encrypted = sut.Encrypt(plainText);
        var decrypted = sut.Decrypt(encrypted);

        decrypted.Should().Be(plainText);
    }
}
