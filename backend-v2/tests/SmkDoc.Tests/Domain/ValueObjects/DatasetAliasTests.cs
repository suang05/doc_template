using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class DatasetAliasTests
{
    [Theory]
    [InlineData("orders", "orders")]
    [InlineData("customer_info", "customer_info")]
    [InlineData("ORDER-HEADER", "order-header")]
    [InlineData("  items123  ", "items123")]
    public void Create_WithValidAlias_NormalizesToLowercaseAndTrims(string input, string expected)
    {
        var alias = DatasetAlias.Create(input);

        alias.Value.Should().Be(expected);
        alias.ToString().Should().Be(expected);
        ((string?)alias).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespace_ThrowsDomainValidationException(string? invalid)
    {
        var act = () => DatasetAlias.Create(invalid!);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Theory]
    [InlineData("invalid alias!")]
    [InlineData("alias with spaces")]
    [InlineData("orders@sales")]
    [InlineData("orders#1")]
    public void Create_WithInvalidCharacters_ThrowsDomainValidationException(string invalid)
    {
        var act = () => DatasetAlias.Create(invalid);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*Only lowercase letters, digits, hyphens, and underscores are allowed.*");
    }

    [Fact]
    public void Create_ExceedingMaxLength_ThrowsDomainValidationException()
    {
        var longAlias = new string('a', DatasetAlias.MaxLength + 1);
        var act = () => DatasetAlias.Create(longAlias);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {DatasetAlias.MaxLength} characters*");
    }

    [Fact]
    public void Equality_CaseInsensitiveEquivalent_AreEqual()
    {
        var a = DatasetAlias.Create("customers");
        var b = DatasetAlias.Create("CUSTOMERS");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = DatasetAlias.Create("items");
        var b = DatasetAlias.Create("orders");

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }
}
