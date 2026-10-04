using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public class ValueObjectTests
{
    // ── Sha256Hash Zero-Allocation Hex Validation ───────────────────────────

    [Fact]
    public void Sha256Hash_ValidHex_Succeeds()
    {
        var validHex = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var hash = new Sha256Hash(validHex);
        hash.Value.Should().Be(validHex);
    }

    [Theory]
    [InlineData("too_short")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("")]
    public void Sha256Hash_InvalidHex_ThrowsDomainValidationException(string invalidInput)
    {
        Action act = () => new Sha256Hash(invalidInput);
        act.Should().Throw<DomainValidationException>();
    }

    // ── DataSourceType Validation ──────────────────────────────────────────

    [Fact]
    public void DataSourceType_ValidValues_Succeed()
    {
        DataSourceType.FromString("json").Should().Be(DataSourceType.Json);
        DataSourceType.FromString("sql").Should().Be(DataSourceType.Sql);
        DataSourceType.FromString("JSON").Should().Be(DataSourceType.Json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("oracle")]
    [InlineData("csv")]
    public void DataSourceType_InvalidValues_ThrowsDomainValidationException(string invalidInput)
    {
        Action act = () => DataSourceType.FromString(invalidInput);
        act.Should().Throw<DomainValidationException>();
    }
}
