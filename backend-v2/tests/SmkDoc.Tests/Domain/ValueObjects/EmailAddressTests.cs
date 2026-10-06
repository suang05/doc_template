using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class EmailAddressTests
{
    [Theory]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("  Admin@Domain.COM  ", "admin@domain.com")]
    [InlineData("firstname.lastname+tag@company.co.th", "firstname.lastname+tag@company.co.th")]
    public void Create_WithValidEmail_NormalizesAndTrims(string input, string expected)
    {
        var email = EmailAddress.Create(input);

        email.Value.Should().Be(expected);
        email.ToString().Should().Be(expected);
        ((string)email).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespace_ThrowsDomainValidationException(string? invalid)
    {
        var act = () => EmailAddress.Create(invalid);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Theory]
    [InlineData("plainaddress")]
    [InlineData("@missingusername.com")]
    [InlineData("missing.domain@")]
    [InlineData("spaces in@email.com")]
    [InlineData("two@@signs.com")]
    public void Create_WithInvalidFormat_ThrowsDomainValidationException(string invalidEmail)
    {
        var act = () => EmailAddress.Create(invalidEmail);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*invalid format*");
    }

    [Fact]
    public void Create_ExceedingMaxLength_ThrowsDomainValidationException()
    {
        var longEmail = new string('a', 250) + "@domain.com"; // > 256 chars
        var act = () => EmailAddress.Create(longEmail);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {EmailAddress.MaxLength} characters*");
    }

    [Fact]
    public void Equality_CaseInsensitiveSameValue_AreEqual()
    {
        var a = EmailAddress.Create("Test@Example.COM");
        var b = EmailAddress.Create("test@example.com");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = EmailAddress.Create("user1@example.com");
        var b = EmailAddress.Create("user2@example.com");

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }
}
