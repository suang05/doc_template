using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using SmkDoc.Infrastructure.Security;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Security;

public class DataProtectionServiceTests
{
    [Fact]
    public void EncryptAndDecrypt_ShouldRestoreOriginalPlainText()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["EncryptionKey"]).Returns("my_super_secret_test_key_32_bytes!");

        var sut = new DataProtectionService(configMock.Object);
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
        var configMock = new Mock<IConfiguration>();
        var sut = new DataProtectionService(configMock.Object);

        var result = sut.Encrypt(input!);
        result.Should().Be(input);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Decrypt_EmptyOrNull_ShouldReturnInputAsIs(string? input)
    {
        var configMock = new Mock<IConfiguration>();
        var sut = new DataProtectionService(configMock.Object);

        var result = sut.Decrypt(input!);
        result.Should().Be(input);
    }

    [Fact]
    public void DefaultKey_ShouldWorkWhenConfigurationKeyIsMissing()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["EncryptionKey"]).Returns((string?)null);

        var sut = new DataProtectionService(configMock.Object);
        var plainText = "Sample sensitive text";

        var encrypted = sut.Encrypt(plainText);
        var decrypted = sut.Decrypt(encrypted);

        decrypted.Should().Be(plainText);
    }
}
