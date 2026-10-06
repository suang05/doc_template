using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Factories;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class CompanyTests
{
    private readonly DateTimeOffset _initialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidParameters_InitializesActiveCompany()
    {
        var company = Company.Create(CompanyName.Create("SAMMAKORN"), _initialTime);

        company.Id.Should().NotBe(Guid.Empty);
        company.Name.Value.Should().Be("SAMMAKORN");
        company.IsActive.Should().BeTrue();
        company.CreatedAt.Should().Be(_initialTime);
        company.UpdatedAt.Should().BeNull();
        company.Projects.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsDomainValidationException(string? invalidName)
    {
        var act = () => Company.Create(CompanyName.Create(invalidName!), _initialTime);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void UpdateName_WithNewName_UpdatesNameAndAuditTimestamp()
    {
        var company = Company.Create(CompanyName.Create("Old Name"), _initialTime);
        var updateTime = _initialTime.AddHours(2);
        var newName = CompanyName.Create("New Name");

        company.UpdateName(newName, updateTime);

        company.Name.Should().Be(newName);
        company.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void UpdateName_WithSameName_DoesNotModifyAuditTimestamp()
    {
        var company = Company.Create(CompanyName.Create("Same Name"), _initialTime);
        var updateTime = _initialTime.AddHours(2);

        company.UpdateName(CompanyName.Create("Same Name"), updateTime);

        company.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Deactivate_WhenActive_SetsInactiveAndAuditTimestamp()
    {
        var company = Company.Create(CompanyName.Create("Test Org"), _initialTime);
        var deactTime = _initialTime.AddDays(1);

        company.Deactivate(deactTime);

        company.IsActive.Should().BeFalse();
        company.UpdatedAt.Should().Be(deactTime);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_IsIdempotent()
    {
        var company = Company.Create(CompanyName.Create("Test Org"), _initialTime);
        var deactTime = _initialTime.AddDays(1);
        company.Deactivate(deactTime);

        var nextTime = deactTime.AddHours(1);
        company.Deactivate(nextTime);

        company.IsActive.Should().BeFalse();
        company.UpdatedAt.Should().Be(deactTime);
    }

    [Fact]
    public void Activate_WhenInactive_SetsActiveAndAuditTimestamp()
    {
        var company = Company.Create(CompanyName.Create("Test Org"), _initialTime);
        var deactTime = _initialTime.AddDays(1);
        company.Deactivate(deactTime);

        var reactTime = deactTime.AddDays(1);
        company.Activate(reactTime);

        company.IsActive.Should().BeTrue();
        company.UpdatedAt.Should().Be(reactTime);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_IsIdempotent()
    {
        var company = Company.Create(CompanyName.Create("Test Org"), _initialTime);
        var actTime = _initialTime.AddDays(1);

        company.Activate(actTime);

        company.IsActive.Should().BeTrue();
        company.UpdatedAt.Should().BeNull();
    }
}
