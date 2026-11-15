using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Security;

namespace SmkDoc.Tests.Infrastructure.Security;

public sealed class ApiKeyVerifierTests
{
    private const string ValidPepper = "unit-test-pepper-at-least-16-chars-long";

    [Theory]
    [InlineData("")]
    [InlineData("short-pepper")]
    public void CredentialGenerator_WhenPepperMissingOrTooShort_ThrowsInvalidOperationException(string pepper)
    {
        var sut = new ApiKeyCredentialGenerator(Options.Create(new ApiKeyOptions { ApiKeyPepper = pepper }));

        var act = () => sut.MintLiveKey();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CredentialGenerator_MintLiveKey_GeneratesStructuredKeyAndMatchingHmacDigest()
    {
        var sut = new ApiKeyCredentialGenerator(Options.Create(new ApiKeyOptions { ApiKeyPepper = ValidPepper }));

        var first = sut.MintLiveKey();
        var second = sut.MintLiveKey();

        first.KeyPrefix.Should().Be("smk_live");
        first.KeyId.Should().HaveLength(ApiKey.StructuredKeyIdLength);
        first.KeySuffix.Should().HaveLength(ApiKey.StructuredKeySuffixLength);
        first.PlainTextKey.Should().StartWith($"smk_live_{first.KeyId}_");
        first.PlainTextKey.Should().EndWith(first.KeySuffix);
        first.SecretHash.Length.Should().Be(32);

        string secretPart = first.PlainTextKey[$"smk_live_{first.KeyId}_".Length..];
        byte[] expectedDigest = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(ValidPepper),
            Encoding.UTF8.GetBytes(secretPart));
        first.SecretHash.ToArray().Should().Equal(expectedDigest);

        second.PlainTextKey.Should().NotBe(first.PlainTextKey);
        second.KeyId.Should().NotBe(first.KeyId);

        var entity = ApiKey.IssueStructured(
            ProjectId.New(),
            ApiKeyName.Create("Billing Worker"),
            "billing-api",
            first.KeyPrefix,
            first.KeyId,
            first.KeySuffix,
            first.SecretHash.ToArray(),
            ExpirationPolicy.Never,
            TestConstants.BaselineTime,
            ApiKeyScope.ReadWrite);

        entity.GetMaskedKey().Should().Be($"smk_live_{first.KeyId}_****{first.KeySuffix}");
        entity.KeyHash.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_WhenBlankKey_ReturnsNull(string rawKey)
    {
        var sut = new ApiKeyVerifier(
            Options.Create(new ApiKeyOptions { ApiKeyPepper = ValidPepper, AllowLegacySha256Keys = true }),
            TestConstants.CreateFakeClock());

        var result = await sut.VerifyAsync(rawKey, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task VerifyAsync_WhenKeyExceedsMaxLength_ReturnsNull()
    {
        var sut = new ApiKeyVerifier(
            Options.Create(new ApiKeyOptions { ApiKeyPepper = ValidPepper, AllowLegacySha256Keys = true }),
            TestConstants.CreateFakeClock());

        var oversizedKey = "smk_live_123456789012_" + new string('a', 260);
        var result = await sut.VerifyAsync(oversizedKey, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task VerifyAsync_WhenLegacyDisabledAndNonStructuredKeySupplied_ReturnsNullWithoutDbLookup()
    {
        var sut = new ApiKeyVerifier(
            Options.Create(new ApiKeyOptions { ApiKeyPepper = ValidPepper, AllowLegacySha256Keys = false }),
            TestConstants.CreateFakeClock());

        var result = await sut.VerifyAsync("smk_legacy_raw_key_token_1234567890", CancellationToken.None);

        result.Should().BeNull();
    }
}
