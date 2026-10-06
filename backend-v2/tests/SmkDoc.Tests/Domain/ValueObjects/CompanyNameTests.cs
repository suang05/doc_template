using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class CompanyNameTests
{
    [Theory]
    [InlineData("SAMMAKORN", "SAMMAKORN")]
    [InlineData("  Sammakorn Public Co., Ltd.  ", "Sammakorn Public Co., Ltd.")]
    [InlineData("บริษัท สัมมากร จำกัด", "บริษัท สัมมากร จำกัด")]
    public void Create_WithValidName_TrimsAndCreates(string input, string expected)
    {
        var name = CompanyName.Create(input);

        name.Value.Should().Be(expected);
        name.ToString().Should().Be(expected);
        ((string)name).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespace_ThrowsDomainValidationException(string? invalid)
    {
        var act = () => CompanyName.Create(invalid);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Fact]
    public void Create_ExceedingMaxLength_ThrowsDomainValidationException()
    {
        var longString = new string('C', CompanyName.MaxLength + 1);
        var act = () => CompanyName.Create(longString);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {CompanyName.MaxLength} characters*");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = CompanyName.Create("Sammakorn");
        var b = CompanyName.Create("Sammakorn");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = CompanyName.Create("Company A");
        var b = CompanyName.Create("Company B");

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }
}
