using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Factories;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class ProjectTests
{
    private readonly DateTimeOffset _initialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_InitializesActiveProject()
    {
        var project = Project.Create(_companyId, ProjectName.Create("Billing System"), TemplateSlug.Create("billing-sys"), _initialTime);

        project.Id.Should().NotBe(Guid.Empty);
        project.CompanyId.Should().Be(_companyId);
        project.Name.Value.Should().Be("Billing System");
        project.Slug.Value.Should().Be("billing-sys");
        project.IsActive.Should().BeTrue();
        project.CreatedAt.Should().Be(_initialTime);
        project.UpdatedAt.Should().BeNull();
        project.Templates.Should().BeEmpty();
        project.ApiKeys.Should().BeEmpty();
        project.UserRoles.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainValidationException()
    {
        var act = () => Project.Create(Guid.Empty, ProjectName.Create("Name"), TemplateSlug.Create("slug"), _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsDomainValidationException(string? invalidName)
    {
        var act = () => Project.Create(_companyId, ProjectName.Create(invalidName!), TemplateSlug.Create("valid-slug"), _initialTime);

        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")] // Too short (< 2)
    [InlineData("INVALID_SLUG!")]
    public void Create_WithInvalidSlug_ThrowsDomainValidationException(string? invalidSlug)
    {
        var act = () => Project.Create(_companyId, ProjectName.Create("Valid Name"), TemplateSlug.Create(invalidSlug!), _initialTime);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void UpdateName_WithNewName_UpdatesNameAndAuditTimestamp()
    {
        var project = Project.Create(_companyId, ProjectName.Create("Old Name"), TemplateSlug.Create("slug-test"), _initialTime);
        var updateTime = _initialTime.AddHours(3);
        var newName = ProjectName.Create("New Name");

        project.UpdateName(newName, updateTime);

        project.Name.Should().Be(newName);
        project.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void UpdateName_WithSameName_DoesNotModifyAuditTimestamp()
    {
        var project = Project.Create(_companyId, ProjectName.Create("Same Name"), TemplateSlug.Create("slug-test"), _initialTime);
        var updateTime = _initialTime.AddHours(3);

        project.UpdateName(ProjectName.Create("Same Name"), updateTime);

        project.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Deactivate_WhenActive_SetsInactiveAndAuditTimestamp()
    {
        var project = Project.Create(_companyId, ProjectName.Create("Test Proj"), TemplateSlug.Create("test-proj"), _initialTime);
        var deactTime = _initialTime.AddDays(1);

        project.Deactivate(deactTime);

        project.IsActive.Should().BeFalse();
        project.UpdatedAt.Should().Be(deactTime);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_IsIdempotent()
    {
        var project = Project.Create(_companyId, ProjectName.Create("Test Proj"), TemplateSlug.Create("test-proj"), _initialTime);
        var deactTime = _initialTime.AddDays(1);
        project.Deactivate(deactTime);

        var nextTime = deactTime.AddHours(2);
        project.Deactivate(nextTime);

        project.IsActive.Should().BeFalse();
        project.UpdatedAt.Should().Be(deactTime);
    }

    [Fact]
    public void Activate_WhenInactive_SetsActiveAndAuditTimestamp()
    {
        var project = Project.Create(_companyId, ProjectName.Create("Test Proj"), TemplateSlug.Create("test-proj"), _initialTime);
        var deactTime = _initialTime.AddDays(1);
        project.Deactivate(deactTime);

        var reactTime = deactTime.AddDays(1);
        project.Activate(reactTime);

        project.IsActive.Should().BeTrue();
        project.UpdatedAt.Should().Be(reactTime);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_IsIdempotent()
    {
        var project = Project.Create(_companyId, ProjectName.Create("Test Proj"), TemplateSlug.Create("test-proj"), _initialTime);
        var actTime = _initialTime.AddDays(1);

        project.Activate(actTime);

        project.IsActive.Should().BeTrue();
        project.UpdatedAt.Should().BeNull();
    }
}
