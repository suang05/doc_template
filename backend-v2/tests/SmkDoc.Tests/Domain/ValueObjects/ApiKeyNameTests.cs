using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class ApiKeyNameTests
{
    [Theory]
    [InlineData("My API Key", "My API Key")]
    [InlineData("  CRM Integration  ", "CRM Integration")]
    public void Create_WithValidString_TrimsAndCreates(string input, string expected)
    {
        var name = ApiKeyName.Create(input);
        name.Value.Should().Be(expected);
        name.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespace_ThrowsDomainValidationException(string? invalid)
    {
        var act = () => ApiKeyName.Create(invalid);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Fact]
    public void Create_ExceedingMaxLength_ThrowsDomainValidationException()
    {
        var longString = new string('x', ApiKeyName.MaxLength + 1);
        var act = () => ApiKeyName.Create(longString);
        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {ApiKeyName.MaxLength} characters*");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = ApiKeyName.Create("ERP Key");
        var b = ApiKeyName.Create("ERP Key");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = ApiKeyName.Create("ERP Key");
        var b = ApiKeyName.Create("CRM Key");

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
    }
}
