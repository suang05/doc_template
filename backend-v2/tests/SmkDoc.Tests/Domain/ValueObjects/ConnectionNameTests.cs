using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class ConnectionNameTests
{
    [Theory]
    [InlineData("PostgreSQL Production", "PostgreSQL Production")]
    [InlineData("  Data Warehouse DB  ", "Data Warehouse DB")]
    [InlineData("ระบบฐานข้อมูลหลัก", "ระบบฐานข้อมูลหลัก")]
    public void Create_WithValidName_TrimsAndCreates(string input, string expected)
    {
        var name = ConnectionName.Create(input);

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
        var act = () => ConnectionName.Create(invalid);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Fact]
    public void Create_ExceedingMaxLength_ThrowsDomainValidationException()
    {
        var longString = new string('C', ConnectionName.MaxLength + 1);
        var act = () => ConnectionName.Create(longString);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {ConnectionName.MaxLength} characters*");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = ConnectionName.Create("Main DB");
        var b = ConnectionName.Create("Main DB");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = ConnectionName.Create("Connection A");
        var b = ConnectionName.Create("Connection B");

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }
}
