using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class DatasetNameTests
{
    [Theory]
    [InlineData("Customer Orders", "Customer Orders")]
    [InlineData("  Billing History  ", "Billing History")]
    [InlineData("รายการยอดขายประจำวัน", "รายการยอดขายประจำวัน")]
    public void Create_WithValidName_TrimsAndCreates(string input, string expected)
    {
        var name = DatasetName.Create(input);

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
        var act = () => DatasetName.Create(invalid);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Fact]
    public void Create_ExceedingMaxLength_ThrowsDomainValidationException()
    {
        var longString = new string('D', DatasetName.MaxLength + 1);
        var act = () => DatasetName.Create(longString);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {DatasetName.MaxLength} characters*");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = DatasetName.Create("Orders");
        var b = DatasetName.Create("Orders");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = DatasetName.Create("Dataset A");
        var b = DatasetName.Create("Dataset B");

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }
}
