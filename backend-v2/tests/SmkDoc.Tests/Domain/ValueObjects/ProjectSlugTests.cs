using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public sealed class ProjectSlugTests
{
    [Theory]
    [InlineData("billing-system")]
    [InlineData("tenant-alpha")]
    [InlineData("proj123")]
    [InlineData("a-b-c-d")]
    public void Constructor_WithValidSlug_SetsNormalizedValue(string input)
    {
        var slug = new ProjectSlug(input);

        slug.Value.Should().Be(input.ToLowerInvariant());
        ((string)slug).Should().Be(input.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithNullOrWhitespace_ThrowsDomainValidationException(string? input)
    {
        var act = () => new ProjectSlug(input!);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Theory]
    [InlineData("a")] // Too short (< 2)
    [InlineData("invalid slug with spaces")]
    [InlineData("slug_with_underscores")]
    [InlineData("slug--double-hyphen")]
    [InlineData("-starts-with-hyphen")]
    [InlineData("ends-with-hyphen-")]
    [InlineData("UPPERCASE!@#")]
    public void Constructor_WithInvalidFormat_ThrowsDomainValidationException(string input)
    {
        var act = () => new ProjectSlug(input);
        act.Should().Throw<DomainValidationException>();
    }
}
