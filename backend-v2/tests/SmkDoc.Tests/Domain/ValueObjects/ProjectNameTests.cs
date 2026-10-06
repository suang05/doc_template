using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class ProjectNameTests
{
    [Theory]
    [InlineData("Billing System", "Billing System")]
    [InlineData("  Customer Relationship Management  ", "Customer Relationship Management")]
    [InlineData("ระบบออกใบกำกับภาษี", "ระบบออกใบกำกับภาษี")]
    public void Create_WithValidName_TrimsAndCreates(string input, string expected)
    {
        var name = ProjectName.Create(input);

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
        var act = () => ProjectName.Create(invalid);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Fact]
    public void Create_ExceedingMaxLength_ThrowsDomainValidationException()
    {
        var longString = new string('P', ProjectName.MaxLength + 1);
        var act = () => ProjectName.Create(longString);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {ProjectName.MaxLength} characters*");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = ProjectName.Create("ERP Project");
        var b = ProjectName.Create("ERP Project");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = ProjectName.Create("Project A");
        var b = ProjectName.Create("Project B");

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }
}
