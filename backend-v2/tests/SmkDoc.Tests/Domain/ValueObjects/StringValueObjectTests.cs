using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects;

public sealed class StringValueObjectTests
{
    public static readonly TheoryData<Func<string?, object>, int> StringValueObjectFactories = new()
    {
        { s => ApiKeyName.Create(s), ApiKeyName.MaxLength },
        { s => CompanyName.Create(s), CompanyName.MaxLength },
        { s => ConnectionName.Create(s), ConnectionName.MaxLength },
        { s => DatasetName.Create(s), DatasetName.MaxLength },
        { s => ProjectName.Create(s), ProjectName.MaxLength },
        { s => TemplateName.Create(s), TemplateName.MaxLength },
        { s => DocumentReference.Create(s), DocumentReference.MaxLength }
    };

    [Theory]
    [MemberData(nameof(StringValueObjectFactories), DisableDiscoveryEnumeration = true)]
    public void Create_WithValidString_TrimsAndSetsValue(Func<string?, object> factory, int _)
    {
        var result1 = factory("  Valid Value  ");
        result1.ToString().Should().Be("Valid Value");

        var resultThai = factory("  บริษัท สัมมากร จำกัด  ");
        resultThai.ToString().Should().Be("บริษัท สัมมากร จำกัด");
    }

    [Theory]
    [MemberData(nameof(StringValueObjectFactories), DisableDiscoveryEnumeration = true)]
    public void Create_WithNullOrWhitespace_ThrowsDomainValidationException(Func<string?, object> factory, int _)
    {
        var actNull = () => factory(null);
        var actEmpty = () => factory("");
        var actWhitespace = () => factory("   ");

        actNull.Should().Throw<DomainValidationException>().WithMessage("*cannot be empty or whitespace*");
        actEmpty.Should().Throw<DomainValidationException>().WithMessage("*cannot be empty or whitespace*");
        actWhitespace.Should().Throw<DomainValidationException>().WithMessage("*cannot be empty or whitespace*");
    }

    [Theory]
    [MemberData(nameof(StringValueObjectFactories), DisableDiscoveryEnumeration = true)]
    public void Create_ExceedingMaxLength_ThrowsDomainValidationException(Func<string?, object> factory, int maxLength)
    {
        var longString = new string('x', maxLength + 1);
        var act = () => factory(longString);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {maxLength} characters*");
    }
}
