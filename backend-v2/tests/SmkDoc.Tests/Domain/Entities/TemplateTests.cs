using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class TemplateTests
{
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly DateTimeOffset _initialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private Template CreateTemplate(string name = "Standard Invoice", string slug = "std-invoice", string? category = null, DateTimeOffset? now = null) =>
        Template.Create(_projectId, TemplateName.Create(name), TemplateSlug.Create(slug), category, now ?? _initialTime);

    [Fact]
    public void Create_WithValidParameters_InitializesActiveTemplate()
    {
        var template = CreateTemplate("Standard Invoice", "std-invoice", now: _initialTime);

        template.Id.Should().NotBe(Guid.Empty);
        template.ProjectId.Should().Be(_projectId);
        template.Name.Value.Should().Be("Standard Invoice");
        template.Slug.Value.Should().Be("std-invoice");
        template.Category.Should().BeNull();
        template.IsActive.Should().BeTrue();
        template.CurrentVersionId.Should().BeNull();
        template.CreatedAt.Should().Be(_initialTime);
        template.UpdatedAt.Should().BeNull();
        template.Versions.Should().BeEmpty();
        template.FieldMappings.Should().BeEmpty();
        template.TemplateDatasets.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => Template.Create(Guid.Empty, TemplateName.Create("Name"), TemplateSlug.Create("slug"), null, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public void UpdateDetails_UpdatesNameAndCategory_WithAuditTimestamp()
    {
        var template = CreateTemplate("Old Name", "slug-test", now: _initialTime);
        var updateTime = _initialTime.AddHours(2);

        template.UpdateDetails(TemplateName.Create("New Name"), "Billing", updateTime);

        template.Name.Value.Should().Be("New Name");
        template.Category.Should().Be("Billing");
        template.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void Deactivate_And_Activate_MutateStateWithAuditTimestamp()
    {
        var template = CreateTemplate("Receipt", "receipt-test", now: _initialTime);
        var deactTime = _initialTime.AddHours(1);

        template.Deactivate(deactTime);
        template.IsActive.Should().BeFalse();
        template.UpdatedAt.Should().Be(deactTime);

        var actTime = _initialTime.AddHours(2);
        template.Activate(actTime);
        template.IsActive.Should().BeTrue();
        template.UpdatedAt.Should().Be(actTime);
    }

    [Fact]
    public void CreateDraftVersion_CreatesAndAppendsVersion()
    {
        var template = CreateTemplate("Contract", "contract-test", now: _initialTime);
        var versionTime = _initialTime.AddMinutes(30);

        var draft = template.CreateDraftVersion(1, "templates/contract.html", TemplateFormat.Html, "author", versionTime, "First draft");

        draft.Should().NotBeNull();
        draft.TemplateId.Should().Be(template.Id);
        draft.Version.Should().Be(1);
        draft.Status.Should().Be(TemplateVersionStatus.Draft);
        draft.StorageKey.Should().Be("templates/contract.html");
        draft.CommitMessage.Should().Be("First draft");

        template.Versions.Should().ContainSingle().Which.Should().BeSameAs(draft);
        template.UpdatedAt.Should().Be(versionTime);
    }

    [Fact]
    public void CreateDraftVersion_WithExistingVersionNumber_ThrowsDuplicateVersionException()
    {
        var template = CreateTemplate("Contract", "contract-test", now: _initialTime);
        template.CreateDraftVersion(1, "templates/v1.html", TemplateFormat.Html, "author", _initialTime);

        var act = () => template.CreateDraftVersion(1, "templates/v1-dupe.html", TemplateFormat.Html, "author", _initialTime);

        var ex = act.Should().Throw<DuplicateVersionException>();
        ex.Which.ErrorCode.Should().Be("DUPLICATE_VERSION");
    }
}
