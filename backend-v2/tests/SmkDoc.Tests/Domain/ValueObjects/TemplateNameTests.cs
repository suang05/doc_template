using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class TemplateNameTests
{
    [Theory]
    [InlineData("Invoice Template", "Invoice Template")]
    [InlineData("  Official Receipt  ", "Official Receipt")]
    [InlineData("แบบฟอร์มสัญญาจ้าง", "แบบฟอร์มสัญญาจ้าง")]
    public void Create_WithValidName_TrimsAndCreates(string input, string expected)
    {
        var name = TemplateName.Create(input);

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
        var act = () => TemplateName.Create(invalid);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Fact]
    public void Create_ExceedingMaxLength_ThrowsDomainValidationException()
    {
        var longString = new string('T', TemplateName.MaxLength + 1);
        var act = () => TemplateName.Create(longString);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {TemplateName.MaxLength} characters*");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = TemplateName.Create("Tax Invoice");
        var b = TemplateName.Create("Tax Invoice");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = TemplateName.Create("Template A");
        var b = TemplateName.Create("Template B");

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }
}
