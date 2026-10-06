using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class DocumentReferenceTests
{
    [Theory]
    [InlineData("INV-2026-0001", "INV-2026-0001")]
    [InlineData("  SC-001  ", "SC-001")]
    [InlineData("CONTRACT/SAM/2026/01", "CONTRACT/SAM/2026/01")]
    public void Create_WithValidReference_TrimsAndCreates(string input, string expected)
    {
        var docRef = DocumentReference.Create(input);

        docRef.Value.Should().Be(expected);
        docRef.ToString().Should().Be(expected);
        ((string)docRef).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespace_ThrowsDomainValidationException(string? invalid)
    {
        var act = () => DocumentReference.Create(invalid);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Fact]
    public void Create_ExceedingMaxLength_ThrowsDomainValidationException()
    {
        var longRef = new string('D', DocumentReference.MaxLength + 1);
        var act = () => DocumentReference.Create(longRef);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {DocumentReference.MaxLength} characters*");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = DocumentReference.Create("INV-001");
        var b = DocumentReference.Create("INV-001");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = DocumentReference.Create("INV-001");
        var b = DocumentReference.Create("INV-002");

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_FromStringAndToString_Works()
    {
        DocumentReference docRef = "REF-100";
        string str = docRef;

        str.Should().Be("REF-100");
    }
}
