using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Domain.Entities;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Infrastructure.Contexts;

public sealed class ConcurrencyContextTests
{
    private static ConcurrencyContext CreateSut() => new();

    [Theory]
    [InlineData("\"1700000000\"", "1700000000")]
    [InlineData("W/\"1700000000\"", "1700000000")]
    [InlineData("  \"54321\"  ", "54321")]
    [InlineData("*", "*")]
    public void SetIfMatch_WhenValidHeaderProvided_ShouldSanitizeQuotesAndWeakPrefixes(string rawHeader, string expectedToken)
    {
        // Step 1: Arrange
        var sut = CreateSut();

        // Step 2: Act
        sut.SetIfMatch(rawHeader);

        // Step 3: Assert
        sut.HasIfMatch.Should().BeTrue();
        sut.IfMatch.Should().Be(expectedToken);
    }

    [Fact]
    public void EnsureMatches_WhenIfMatchMatchesEntityToken_ShouldMarkVerifiedWithoutThrowing()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        Template template = TemplateBuilder.ATemplate().WithName("Invoice").WithSlug("invoice");
        sut.SetIfMatch($"\"{template.GetConcurrencyToken()}\"");

        // Step 2: Act
        var act = () => sut.EnsureMatches(template);

        // Step 3: Assert
        act.Should().NotThrow();
        sut.IsVerified.Should().BeTrue();
    }

    [Fact]
    public void EnsureMatches_WhenIfMatchIsWildcard_ShouldBypassCheckAndMarkVerified()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        Template template = TemplateBuilder.ATemplate().WithName("Invoice").WithSlug("invoice");
        sut.SetIfMatch("*");

        // Step 2: Act
        var act = () => sut.EnsureMatches(template);

        // Step 3: Assert
        act.Should().NotThrow();
        sut.IsVerified.Should().BeTrue();
    }

    [Fact]
    public void EnsureMatches_WhenIfMatchDoesNotMatch_ShouldThrowPreconditionFailedException()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        Template template = TemplateBuilder.ATemplate().WithName("Invoice").WithSlug("invoice");
        sut.SetIfMatch("\"stale-token-999\"");

        // Step 2: Act
        var act = () => sut.EnsureMatches(template);

        // Step 3: Assert
        act.Should().Throw<PreconditionFailedException>();
        sut.IsVerified.Should().BeFalse();
    }
}

