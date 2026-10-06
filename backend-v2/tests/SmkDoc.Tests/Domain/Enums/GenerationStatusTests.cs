using FluentAssertions;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests.Domain.Enums;

public class GenerationStatusTests
{
    [Fact]
    public void GetAll_ReturnsAllDefinedStatuses()
    {
        var statuses = Enumeration.GetAll<GenerationStatus>().ToList();

        statuses.Should().HaveCount(5);
        statuses.Should().Contain(GenerationStatus.Success);
        statuses.Should().Contain(GenerationStatus.Failed);
        statuses.Should().Contain(GenerationStatus.Processing);
        statuses.Should().Contain(GenerationStatus.Timeout);
        statuses.Should().Contain(GenerationStatus.ValidationFailed);
    }

    [Theory]
    [InlineData("SUCCESS", 1)]
    [InlineData("FAILED", 2)]
    [InlineData("PROCESSING", 3)]
    [InlineData("TIMEOUT", 4)]
    [InlineData("VALIDATION_FAILED", 5)]
    public void FromName_WithValidName_ReturnsExpectedStatus(string name, int expectedId)
    {
        var status = GenerationStatus.FromName(name);

        status.Id.Should().Be(expectedId);
        status.Name.Should().Be(name);
        ((string)status).Should().Be(name);
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("INVALID")]
    [InlineData("")]
    public void FromName_WithInvalidName_ThrowsDomainValidationException(string invalid)
    {
        var act = () => GenerationStatus.FromName(invalid);

        act.Should().Throw<DomainValidationException>();
    }
}
