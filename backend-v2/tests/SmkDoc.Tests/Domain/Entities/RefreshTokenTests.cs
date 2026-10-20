using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class RefreshTokenTests
{
    private static readonly Sha256Hash ValidHash = new(new string('a', 64));
    private static readonly Sha256Hash ReplacementHash = new(new string('b', 64));
    private static readonly DateTimeOffset FixedNow = TestConstants.BaselineTime;

    [Fact]
    public void Create_WithValidParameters_CreatesActiveToken()
    {
        var userId = Guid.NewGuid();
        var expiresAt = FixedNow.AddDays(7);

        var token = RefreshToken.Create(userId, ValidHash, expiresAt, FixedNow);

        token.Id.Should().NotBeEmpty();
        token.UserId.Should().Be(userId);
        token.TokenHash.Should().Be(ValidHash);
        token.ExpiresAt.Should().Be(expiresAt);
        token.CreatedAt.Should().Be(FixedNow);
        token.RevokedAt.Should().BeNull();
        token.ReplacedByTokenHash.Should().BeNull();
        token.IsRevoked.Should().BeFalse();
        token.IsExpired(FixedNow).Should().BeFalse();
        token.IsActive(FixedNow).Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsDomainValidationException()
    {
        var act = () => RefreshToken.Create(Guid.Empty, ValidHash, FixedNow.AddDays(7), FixedNow);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Create_WithExpirationInPastOrNow_ThrowsDomainValidationException()
    {
        var act = () => RefreshToken.Create(Guid.NewGuid(), ValidHash, FixedNow, FixedNow);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*expiration must be strictly in the future*");
    }

    [Fact]
    public void IsExpired_WhenCurrentTimePastExpiresAt_ReturnsTrue()
    {
        var expiresAt = FixedNow.AddDays(7);
        var token = RefreshToken.Create(Guid.NewGuid(), ValidHash, expiresAt, FixedNow);

        token.IsExpired(expiresAt.AddSeconds(1)).Should().BeTrue();
        token.IsActive(expiresAt.AddSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void Revoke_WhenActive_RevokesAndSetsReplacementHash()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), ValidHash, FixedNow.AddDays(7), FixedNow);
        var revokeTime = FixedNow.AddHours(1);

        token.Revoke(revokeTime, ReplacementHash);

        token.IsRevoked.Should().BeTrue();
        token.RevokedAt.Should().Be(revokeTime);
        token.ReplacedByTokenHash.Should().Be(ReplacementHash);
        token.IsActive(revokeTime).Should().BeFalse();
        token.UpdatedAt.Should().Be(revokeTime);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsBusinessRuleViolationException()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), ValidHash, FixedNow.AddDays(7), FixedNow);
        token.Revoke(FixedNow.AddMinutes(5));

        var act = () => token.Revoke(FixedNow.AddMinutes(10));

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already been revoked*");
    }

    [Fact]
    public void IsWithinReuseLeeway_WhenNotRevoked_ReturnsFalse()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), ValidHash, FixedNow.AddDays(7), FixedNow);

        token.IsWithinReuseLeeway(FixedNow.AddMinutes(1), 30).Should().BeFalse();
    }

    [Fact]
    public void IsWithinReuseLeeway_WhenRevokedWithinLeeway_ReturnsTrue()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), ValidHash, FixedNow.AddDays(7), FixedNow);
        var revokeTime = FixedNow.AddMinutes(5);
        token.Revoke(revokeTime, ReplacementHash);

        // Evaluation 10 seconds after revocation (<= 30s leeway)
        token.IsWithinReuseLeeway(revokeTime.AddSeconds(10), 30).Should().BeTrue();
        // Evaluation exactly at 30 seconds
        token.IsWithinReuseLeeway(revokeTime.AddSeconds(30), 30).Should().BeTrue();
    }

    [Fact]
    public void IsWithinReuseLeeway_WhenRevokedPastLeeway_ReturnsFalse()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), ValidHash, FixedNow.AddDays(7), FixedNow);
        var revokeTime = FixedNow.AddMinutes(5);
        token.Revoke(revokeTime, ReplacementHash);

        // Evaluation 31 seconds after revocation (> 30s leeway)
        token.IsWithinReuseLeeway(revokeTime.AddSeconds(31), 30).Should().BeFalse();
    }
}
