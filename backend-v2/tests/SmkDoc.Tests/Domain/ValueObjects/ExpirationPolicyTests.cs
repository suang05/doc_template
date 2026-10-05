using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class ExpirationPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Never_HasNullExpiresAt_AndNeverExpires()
    {
        var policy = ExpirationPolicy.Never;

        policy.ExpiresAt.Should().BeNull();
        policy.IsExpiredAt(Now.AddYears(100)).Should().BeFalse();
        policy.ToString().Should().Be("Never");
    }

    [Fact]
    public void Until_WithFutureDate_CreatesPolicy()
    {
        var future = Now.AddDays(7);
        var policy = ExpirationPolicy.Until(future, Now);

        policy.ExpiresAt.Should().Be(future);
        policy.IsExpiredAt(Now.AddDays(6)).Should().BeFalse();
        policy.IsExpiredAt(Now.AddDays(7)).Should().BeTrue();
        policy.IsExpiredAt(Now.AddDays(8)).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-3600)]
    public void Until_WithPastOrPresentDate_ThrowsDomainValidationException(int secondOffset)
    {
        var invalidDate = Now.AddSeconds(secondOffset);
        var act = () => ExpirationPolicy.Until(invalidDate, Now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*expiration must be in the future*")
            .Which.ErrorCode.Should().Be("API_KEY_EXPIRY_IN_PAST");
    }

    [Fact]
    public void FromExisting_AllowsMaterializingExistingDateWithoutValidation()
    {
        var past = Now.AddDays(-10);
        var policy = ExpirationPolicy.FromExisting(past);

        policy.ExpiresAt.Should().Be(past);
        policy.IsExpiredAt(Now).Should().BeTrue();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var date = Now.AddDays(30);
        var p1 = ExpirationPolicy.Until(date, Now);
        var p2 = ExpirationPolicy.Until(date, Now);

        p1.Should().Be(p2);
        (p1 == p2).Should().BeTrue();
        p1.GetHashCode().Should().Be(p2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var p1 = ExpirationPolicy.Until(Now.AddDays(10), Now);
        var p2 = ExpirationPolicy.Until(Now.AddDays(20), Now);

        p1.Should().NotBe(p2);
        (p1 == p2).Should().BeFalse();
    }
}
