using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class TemplateTests
{
    private readonly ProjectId _projectId = ProjectId.New();
    private readonly DateTimeOffset _initialTime = TestConstants.BaselineTime;

    private Template CreateTemplate(string name = "Standard Invoice", string slug = "std-invoice", string? category = null, DateTimeOffset? now = null) =>
        Template.Create(_projectId, TemplateName.Create(name), TemplateSlug.Create(slug), category, now ?? _initialTime);

    [Fact]
    public void Create_WithValidParameters_InitializesActiveTemplate()
    {
        var template = CreateTemplate("Standard Invoice", "std-invoice", now: _initialTime);

        template.Id.Value.Should().NotBe(Guid.Empty);
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
        var act = () => Template.Create(ProjectId.Empty, TemplateName.Create("Name"), TemplateSlug.Create("slug"), null, _initialTime);

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
    public void Deactivate_WhenActive_SetsIsActiveFalseAndUpdatesTimestamp()
    {
        var template = CreateTemplate("Receipt", "receipt-test", now: _initialTime);
        var deactTime = _initialTime.AddHours(1);

        template.Deactivate(deactTime);

        template.IsActive.Should().BeFalse();
        template.UpdatedAt.Should().Be(deactTime);
    }

    [Fact]
    public void Activate_WhenInactive_SetsIsActiveTrueAndUpdatesTimestamp()
    {
        var template = CreateTemplate("Receipt", "receipt-test", now: _initialTime);
        template.Deactivate(_initialTime.AddHours(1));
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

    [Fact]
    public void FindVersion_ByVersionNumber_ReturnsMatchingVersionOrNull()
    {
        var template = CreateTemplate("Contract", "contract-test", now: _initialTime);
        var v1 = template.CreateDraftVersion(1, "templates/v1.html", TemplateFormat.Html, "author", _initialTime);
        var v2 = template.CreateDraftVersion(2, "templates/v2.html", TemplateFormat.Html, "author", _initialTime);

        template.FindVersion(1).Should().BeSameAs(v1);
        template.FindVersion(2).Should().BeSameAs(v2);
        template.FindVersion(3).Should().BeNull();
    }

    [Fact]
    public void FindVersion_ByVersionId_ReturnsMatchingVersionOrNull()
    {
        var template = CreateTemplate("Contract", "contract-test", now: _initialTime);
        var v1 = template.CreateDraftVersion(1, "templates/v1.html", TemplateFormat.Html, "author", _initialTime);

        template.FindVersion(v1.Id).Should().BeSameAs(v1);
        template.FindVersion(Guid.NewGuid()).Should().BeNull();
    }

    // ── Version Management ─────────────────────────────────────────────────────

    [Fact]
    public void CreateDraftVersion_ValidParameters_CreatesAndAddsToVersionsCollection()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);

        var version = template.CreateDraftVersion(1, "s3-key-1", TemplateFormat.Html, "author", _initialTime, "Initial commit");

        version.Should().NotBeNull();
        version.TemplateId.Should().Be(template.Id);
        version.Version.Should().Be(1);
        version.StorageKey.Should().Be("s3-key-1");
        version.FileFormat.Should().Be(TemplateFormat.Html);
        version.CommitMessage.Should().Be("Initial commit");
        template.Versions.Should().ContainSingle().Which.Should().BeSameAs(version);
        template.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void CreateDraftVersion_DuplicateVersionNumber_ThrowsDuplicateVersionException()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        template.CreateDraftVersion(1, "key-1", TemplateFormat.Html, "author", _initialTime);

        Action act = () => template.CreateDraftVersion(1, "key-2", TemplateFormat.Html, "author", _initialTime);

        act.Should().Throw<DuplicateVersionException>()
            .WithMessage("*already exists in template*");
    }

    [Fact]
    public void AddVersion_ValidVersion_AddsToVersionsCollection()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        var version = TemplateVersion.Draft(template.Id, 1, "s3-key-1", TemplateFormat.Html, "author", _initialTime);

        template.AddVersion(version, _initialTime);

        template.Versions.Should().ContainSingle().Which.Should().BeSameAs(version);
        template.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddVersion_WhenNull_ThrowsDomainValidationException()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);

        Action actNull = () => template.AddVersion(null!, _initialTime);
        actNull.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void AddVersion_WhenTemplateIdMismatch_ThrowsDomainValidationException()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);

        var foreignVersion = TemplateVersion.Draft(TemplateId.New(), 1, "key", TemplateFormat.Html, "author", _initialTime);
        Action actMismatch = () => template.AddVersion(foreignVersion, _initialTime);
        actMismatch.Should().Throw<DomainValidationException>()
            .WithMessage("*does not match template ID*");
    }

    [Fact]
    public void AddVersion_DuplicateVersionNumber_ThrowsBusinessRuleViolationException()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        var v1 = TemplateVersion.Draft(template.Id, 1, "key-1", TemplateFormat.Html, "author", _initialTime);
        var v2 = TemplateVersion.Draft(template.Id, 1, "key-2", TemplateFormat.Html, "author", _initialTime);

        template.AddVersion(v1, _initialTime);

        Action actDuplicate = () => template.AddVersion(v2, _initialTime);
        var ex = actDuplicate.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already exists in template*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_VERSION");
    }

    [Fact]
    public void SetCurrentVersion_VersionBelongsToTemplate_SetsCurrentVersionId()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        var version = TemplateVersion.Draft(template.Id, 1, "key", TemplateFormat.Html, "author", _initialTime);
        template.AddVersion(version, _initialTime);

        template.SetCurrentVersion(version.Id, _initialTime);

        template.CurrentVersionId.Should().Be(version.Id);
    }

    [Fact]
    public void SetCurrentVersion_VersionNotInTemplate_ThrowsBusinessRuleViolationException()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        var version = TemplateVersion.Draft(template.Id, 1, "key", TemplateFormat.Html, "author", _initialTime);
        template.AddVersion(version, _initialTime);

        var foreignVersionId = Guid.NewGuid();
        Action act = () => template.SetCurrentVersion(foreignVersionId, _initialTime);

        var ex = act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage($"*does not belong to template '{template.Id}'*");
        ex.Which.ErrorCode.Should().Be("VERSION_NOT_IN_TEMPLATE");
    }

    [Fact]
    public void SetCurrentVersion_WhenInactive_ThrowsBusinessRuleViolationException()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        template.Deactivate(_initialTime);

        var act = () => template.SetCurrentVersion(Guid.NewGuid(), _initialTime);
        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("INACTIVE_TEMPLATE");
    }

    [Fact]
    public void SetCurrentVersion_WhenEmptyGuid_ThrowsDomainValidationException()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);

        var act = () => template.SetCurrentVersion(Guid.Empty, _initialTime);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*VersionId cannot be empty*");
    }

    [Fact]
    public void PublishVersion_WithVersionId_PublishesVersionAndSetsCurrentVersionId()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        var version = template.CreateDraftVersion(1, "key-1", TemplateFormat.Html, "author", _initialTime);

        version.Status.Should().Be(SmkDoc.Domain.Enums.TemplateVersionStatus.Draft);

        var pubTime = _initialTime.AddMinutes(10);
        var published = template.PublishVersion(version.Id, pubTime);

        published.Status.Should().Be(SmkDoc.Domain.Enums.TemplateVersionStatus.Published);
        template.CurrentVersionId.Should().Be(version.Id);
        template.UpdatedAt.Should().Be(pubTime);

        template.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TemplatePublishedDomainEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                TemplateId = template.Id,
                ProjectId = template.ProjectId,
                VersionId = version.Id,
                Version = 1,
                OccurredOn = pubTime
            });

        template.ClearDomainEvents();
        template.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void PublishVersion_WithVersionInstance_AddsToCollectionAndPublishes()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        var version = TemplateVersion.Draft(template.Id, 1, "key-inst", TemplateFormat.Html, "author", _initialTime);

        var pubTime = _initialTime.AddMinutes(5);
        template.PublishVersion(version, pubTime);

        version.Status.Should().Be(SmkDoc.Domain.Enums.TemplateVersionStatus.Published);
        template.CurrentVersionId.Should().Be(version.Id);
        template.Versions.Should().ContainSingle().Which.Should().BeSameAs(version);
        template.UpdatedAt.Should().Be(pubTime);
    }

    [Fact]
    public void PublishVersion_WhenInactive_ThrowsBusinessRuleViolationException()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        var version = template.CreateDraftVersion(1, "key-1", TemplateFormat.Html, "author", _initialTime);
        template.Deactivate(_initialTime);

        var act = () => template.PublishVersion(version.Id, _initialTime.AddMinutes(1));

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("INACTIVE_TEMPLATE");
    }

    [Fact]
    public void PublishVersion_VersionNotInTemplate_ThrowsBusinessRuleViolationException()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        var foreignVersionId = Guid.NewGuid();

        var act = () => template.PublishVersion(foreignVersionId, _initialTime);

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("VERSION_NOT_IN_TEMPLATE");
    }

    [Fact]
    public void PublishVersion_MismatchedTemplateId_ThrowsDomainValidationException()
    {
        var template = CreateTemplate("Invoice", "invoice", now: _initialTime);
        var otherTemplateId = TemplateId.New();
        var version = TemplateVersion.Draft(otherTemplateId, 1, "key-foreign", TemplateFormat.Html, "author", _initialTime);

        var act = () => template.PublishVersion(version, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*does not match template ID*");
    }

    // ── Field Mappings ─────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceFieldMappings_ValidMappings_ReplacesCollection()
    {
        var template = CreateTemplate("Receipt", "receipt", now: _initialTime);
        var initial = FieldMapping.Create(template.Id, "name", "customer.name", "Name", true, 1, _initialTime);
        template.AddFieldMapping(initial, _initialTime);

        var replacement = new List<FieldMapping>
        {
            FieldMapping.Create(template.Id, "total", "invoice.total", "Total", true, 1, _initialTime),
            FieldMapping.Create(template.Id, "date", "invoice.date", "Date", false, 2, _initialTime)
        };

        template.ReplaceFieldMappings(replacement, _initialTime);

        template.FieldMappings.Should().HaveCount(2);
        template.FieldMappings.Select(m => m.Placeholder).Should().Contain(["total", "date"]);
    }

    [Fact]
    public void ReplaceFieldMappings_DuplicatePlaceholders_ThrowsBusinessRuleViolationException()
    {
        var template = CreateTemplate("Receipt", "receipt", now: _initialTime);
        var mappings = new List<FieldMapping>
        {
            FieldMapping.Create(template.Id, "customerName", "a", "A", true, 1, _initialTime),
            FieldMapping.Create(template.Id, "CUSTOMERNAME", "b", "B", false, 2, _initialTime)
        };

        Action act = () => template.ReplaceFieldMappings(mappings, _initialTime);

        var ex = act.Should().Throw<BusinessRuleViolationException>();
        ex.Which.ErrorCode.Should().Be("DUPLICATE_PLACEHOLDER");
        ex.Which.Message.Should().Contain("customerName");
    }

    // ── Template Datasets ──────────────────────────────────────────────────────

    [Fact]
    public void AttachDataset_WhenValid_AddsToCollection()
    {
        var template = CreateTemplate("Order", "order", now: _initialTime);
        var dsId = DatasetId.New();

        template.AttachDataset(dsId, DatasetAlias.Create("items"), 1, _initialTime);

        template.TemplateDatasets.Should().ContainSingle();
        template.TemplateDatasets.First().Alias.Value.Should().Be("items");
    }

    [Fact]
    public void DetachDataset_WhenExists_RemovesFromCollection()
    {
        var template = CreateTemplate("Order", "order", now: _initialTime);
        var dsId = DatasetId.New();
        template.AttachDataset(dsId, DatasetAlias.Create("items"), 1, _initialTime);

        template.DetachDataset(dsId, _initialTime);

        template.TemplateDatasets.Should().BeEmpty();
    }

    [Fact]
    public void AttachDataset_DuplicateAlias_ThrowsBusinessRuleViolationException()
    {
        var template = CreateTemplate("Order", "order", now: _initialTime);
        template.AttachDataset(DatasetId.New(), DatasetAlias.Create("header"), 1, _initialTime);

        Action act = () => template.AttachDataset(DatasetId.New(), DatasetAlias.Create("HEADER"), 2, _initialTime);

        var ex = act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already assigned to template*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DATASET_ALIAS");
    }

    [Fact]
    public void ReplaceDatasets_DuplicateAliases_ThrowsBusinessRuleViolationException()
    {
        var template = CreateTemplate("Order", "order", now: _initialTime);
        var datasets = new List<TemplateDataset>
        {
            TemplateDataset.Create(template.Id, DatasetId.New(), DatasetAlias.Create("customers"), 1, _initialTime),
            TemplateDataset.Create(template.Id, DatasetId.New(), DatasetAlias.Create("CUSTOMERS"), 2, _initialTime)
        };

        Action act = () => template.ReplaceDatasets(datasets, _initialTime);

        var ex = act.Should().Throw<BusinessRuleViolationException>();
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DATASET_ALIAS");
        ex.Which.Message.Should().Contain("customers");
    }
}
