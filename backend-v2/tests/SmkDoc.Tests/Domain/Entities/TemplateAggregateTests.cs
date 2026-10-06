using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class TemplateAggregateTests
{
    private readonly Guid _projectId = Guid.NewGuid();

    private Template CreateTemplate(string name = "Invoice", string slug = "invoice", string? category = null, DateTimeOffset? now = null) =>
        Template.Create(_projectId, TemplateName.Create(name), TemplateSlug.Create(slug), category, now ?? DateTimeOffset.UtcNow);

    [Fact]
    public void Collections_ShouldBeReadOnly_AndNotDirectlyMutable()
    {
        var template = CreateTemplate("Contract", "contract", "legal");

        template.Versions.Should().BeAssignableTo<IReadOnlyCollection<TemplateVersion>>();
        template.FieldMappings.Should().BeAssignableTo<IReadOnlyCollection<FieldMapping>>();
        template.TemplateDatasets.Should().BeAssignableTo<IReadOnlyCollection<TemplateDataset>>();

        var now = DateTimeOffset.UtcNow;

        // Verify that casting to mutable list throws or is not supported
        Action actMutateVersions = () => ((IList<TemplateVersion>)template.Versions).Add(
            TemplateVersion.Draft(template.Id, 1, "key", TemplateFormat.Html, "user", now));
        actMutateVersions.Should().Throw<NotSupportedException>();

        Action actMutateMappings = () => ((IList<FieldMapping>)template.FieldMappings).Add(
            FieldMapping.Create(template.Id, "p", "s", "l", false, 1, now));
        actMutateMappings.Should().Throw<NotSupportedException>();

        Action actMutateDatasets = () => ((IList<TemplateDataset>)template.TemplateDatasets).Add(
            TemplateDataset.Create(template.Id, Guid.NewGuid(), DatasetAlias.Create("alias"), 1, now));
        actMutateDatasets.Should().Throw<NotSupportedException>();
    }

    // ── Version Management ─────────────────────────────────────────────────────

    [Fact]
    public void AddVersion_ValidVersion_AddsToVersionsCollection()
    {
        var now = DateTimeOffset.UtcNow;
        var template = CreateTemplate("Invoice", "invoice", now: now);
        var version = TemplateVersion.Draft(template.Id, 1, "s3-key-1", TemplateFormat.Html, "author", now);

        template.AddVersion(version, now);

        template.Versions.Should().ContainSingle().Which.Should().BeSameAs(version);
        template.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddVersion_NullOrTemplateIdMismatch_ThrowsDomainValidationException()
    {
        var now = DateTimeOffset.UtcNow;
        var template = CreateTemplate("Invoice", "invoice", now: now);

        Action actNull = () => template.AddVersion(null!, now);
        actNull.Should().Throw<DomainValidationException>();

        var foreignVersion = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author", now);
        Action actMismatch = () => template.AddVersion(foreignVersion, now);
        actMismatch.Should().Throw<DomainValidationException>()
            .WithMessage("*does not match template ID*");
    }

    [Fact]
    public void AddVersion_DuplicateVersionNumber_ThrowsBusinessRuleViolationException()
    {
        var now = DateTimeOffset.UtcNow;
        var template = CreateTemplate("Invoice", "invoice", now: now);
        var v1 = TemplateVersion.Draft(template.Id, 1, "key-1", TemplateFormat.Html, "author", now);
        var v2 = TemplateVersion.Draft(template.Id, 1, "key-2", TemplateFormat.Html, "author", now);

        template.AddVersion(v1, now);

        Action actDuplicate = () => template.AddVersion(v2, now);
        var ex = actDuplicate.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already exists in template*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_VERSION");
    }

    [Fact]
    public void SetCurrentVersion_VersionBelongsToTemplate_SetsCurrentVersionId()
    {
        var now = DateTimeOffset.UtcNow;
        var template = CreateTemplate("Invoice", "invoice", now: now);
        var version = TemplateVersion.Draft(template.Id, 1, "key", TemplateFormat.Html, "author", now);
        template.AddVersion(version, now);

        template.SetCurrentVersion(version.Id, now);

        template.CurrentVersionId.Should().Be(version.Id);
    }

    [Fact]
    public void SetCurrentVersion_VersionNotInTemplate_ThrowsBusinessRuleViolationException()
    {
        var now = DateTimeOffset.UtcNow;
        var template = CreateTemplate("Invoice", "invoice", now: now);
        var version = TemplateVersion.Draft(template.Id, 1, "key", TemplateFormat.Html, "author", now);
        template.AddVersion(version, now);

        var foreignVersionId = Guid.NewGuid();
        Action act = () => template.SetCurrentVersion(foreignVersionId, now);

        var ex = act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage($"*does not belong to template '{template.Id}'*");
        ex.Which.ErrorCode.Should().Be("VERSION_NOT_IN_TEMPLATE");
    }

    // ── Field Mappings ─────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceFieldMappings_ValidMappings_ReplacesCollection()
    {
        var now = DateTimeOffset.UtcNow;
        var template = CreateTemplate("Receipt", "receipt", now: now);
        var initial = FieldMapping.Create(template.Id, "name", "customer.name", "Name", true, 1, now);
        template.AddFieldMapping(initial, now);

        var replacement = new List<FieldMapping>
        {
            FieldMapping.Create(template.Id, "total", "invoice.total", "Total", true, 1, now),
            FieldMapping.Create(template.Id, "date", "invoice.date", "Date", false, 2, now)
        };

        template.ReplaceFieldMappings(replacement, now);

        template.FieldMappings.Should().HaveCount(2);
        template.FieldMappings.Select(m => m.Placeholder).Should().Contain(["total", "date"]);
    }

    [Fact]
    public void ReplaceFieldMappings_DuplicatePlaceholders_ThrowsBusinessRuleViolationException()
    {
        var now = DateTimeOffset.UtcNow;
        var template = CreateTemplate("Receipt", "receipt", now: now);
        var mappings = new List<FieldMapping>
        {
            FieldMapping.Create(template.Id, "customerName", "a", "A", true, 1, now),
            FieldMapping.Create(template.Id, "CUSTOMERNAME", "b", "B", false, 2, now) // Duplicate case-insensitive
        };

        Action act = () => template.ReplaceFieldMappings(mappings, now);

        var ex = act.Should().Throw<BusinessRuleViolationException>();
        ex.Which.ErrorCode.Should().Be("DUPLICATE_PLACEHOLDER");
        ex.Which.Message.Should().Contain("customerName");
    }

    // ── Template Datasets ──────────────────────────────────────────────────────

    [Fact]
    public void AttachDataset_And_DetachDataset_ModifiesCollection()
    {
        var now = DateTimeOffset.UtcNow;
        var template = CreateTemplate("Order", "order", now: now);
        var dsId = Guid.NewGuid();

        template.AttachDataset(dsId, DatasetAlias.Create("items"), 1, now);

        template.TemplateDatasets.Should().ContainSingle();
        template.TemplateDatasets.First().Alias.Value.Should().Be("items");

        template.DetachDataset(dsId, now);

        template.TemplateDatasets.Should().BeEmpty();
    }

    [Fact]
    public void AttachDataset_DuplicateAlias_ThrowsBusinessRuleViolationException()
    {
        var now = DateTimeOffset.UtcNow;
        var template = CreateTemplate("Order", "order", now: now);
        template.AttachDataset(Guid.NewGuid(), DatasetAlias.Create("header"), 1, now);

        Action act = () => template.AttachDataset(Guid.NewGuid(), DatasetAlias.Create("HEADER"), 2, now);

        var ex = act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already assigned to template*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DATASET_ALIAS");
    }

    [Fact]
    public void ReplaceDatasets_DuplicateAliases_ThrowsBusinessRuleViolationException()
    {
        var now = DateTimeOffset.UtcNow;
        var template = CreateTemplate("Order", "order", now: now);
        var datasets = new List<TemplateDataset>
        {
            TemplateDataset.Create(template.Id, Guid.NewGuid(), DatasetAlias.Create("customers"), 1, now),
            TemplateDataset.Create(template.Id, Guid.NewGuid(), DatasetAlias.Create("CUSTOMERS"), 2, now)
        };

        Action act = () => template.ReplaceDatasets(datasets, now);

        var ex = act.Should().Throw<BusinessRuleViolationException>();
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DATASET_ALIAS");
        ex.Which.Message.Should().Contain("customers");
    }

    // ── Document Aggregate Invariants ──────────────────────────────────────────

    [Fact]
    public void Document_AddVersion_EnforcesEncapsulationAndDuplicateGuards()
    {
        var now = DateTimeOffset.UtcNow;
        var doc = Document.Create(DocumentReference.Create("DOC-001"), null, now);
        var v1 = DocumentVersion.Create(doc.Id, 1, null, null, "First version", "admin", now);

        doc.AddVersion(v1, now);

        doc.Versions.Should().ContainSingle().Which.Should().BeSameAs(v1);

        var duplicateVersion = DocumentVersion.Create(doc.Id, 1, null, null, "Conflict version", "admin", now);
        Action actDuplicate = () => doc.AddVersion(duplicateVersion, now);

        var ex = actDuplicate.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already exists in document*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DOCUMENT_VERSION");
    }
}
