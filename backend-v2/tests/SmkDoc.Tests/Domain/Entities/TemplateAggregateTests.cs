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

    [Fact]
    public void Collections_ShouldBeReadOnly_AndNotDirectlyMutable()
    {
        var template = new Template(_projectId, "Contract", "contract", "legal");

        template.Versions.Should().BeAssignableTo<IReadOnlyCollection<TemplateVersion>>();
        template.FieldMappings.Should().BeAssignableTo<IReadOnlyCollection<FieldMapping>>();
        template.TemplateDatasets.Should().BeAssignableTo<IReadOnlyCollection<TemplateDataset>>();

        // Verify that casting to mutable list throws or is not supported
        Action actMutateVersions = () => ((IList<TemplateVersion>)template.Versions).Add(
            new TemplateVersion(template.Id, 1, "key", TemplateFormat.Html, "user"));
        actMutateVersions.Should().Throw<NotSupportedException>();

        Action actMutateMappings = () => ((IList<FieldMapping>)template.FieldMappings).Add(
            new FieldMapping(template.Id, "p", "s", "l", false, 1));
        actMutateMappings.Should().Throw<NotSupportedException>();

        Action actMutateDatasets = () => ((IList<TemplateDataset>)template.TemplateDatasets).Add(
            new TemplateDataset(template.Id, Guid.NewGuid(), "alias", 1));
        actMutateDatasets.Should().Throw<NotSupportedException>();
    }

    // ── Version Management ─────────────────────────────────────────────────────

    [Fact]
    public void AddVersion_ValidVersion_AddsToVersionsCollection()
    {
        var template = new Template(_projectId, "Invoice", "invoice");
        var version = new TemplateVersion(template.Id, 1, "s3-key-1", TemplateFormat.Html, "author");

        template.AddVersion(version);

        template.Versions.Should().ContainSingle().Which.Should().BeSameAs(version);
        template.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddVersion_NullOrTemplateIdMismatch_ThrowsDomainValidationException()
    {
        var template = new Template(_projectId, "Invoice", "invoice");

        Action actNull = () => template.AddVersion(null!);
        actNull.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be null*");

        var foreignVersion = new TemplateVersion(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author");
        Action actMismatch = () => template.AddVersion(foreignVersion);
        actMismatch.Should().Throw<DomainValidationException>()
            .WithMessage("*does not match template ID*");
    }

    [Fact]
    public void AddVersion_DuplicateVersionNumber_ThrowsBusinessRuleViolationException()
    {
        var template = new Template(_projectId, "Invoice", "invoice");
        var v1 = new TemplateVersion(template.Id, 1, "key-1", TemplateFormat.Html, "author");
        var v2 = new TemplateVersion(template.Id, 1, "key-2", TemplateFormat.Html, "author");

        template.AddVersion(v1);

        Action actDuplicate = () => template.AddVersion(v2);
        var ex = actDuplicate.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already exists in template*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_VERSION");
    }

    [Fact]
    public void SetCurrentVersion_VersionBelongsToTemplate_SetsCurrentVersionId()
    {
        var template = new Template(_projectId, "Invoice", "invoice");
        var version = new TemplateVersion(template.Id, 1, "key", TemplateFormat.Html, "author");
        template.AddVersion(version);

        template.SetCurrentVersion(version.Id);

        template.CurrentVersionId.Should().Be(version.Id);
    }

    [Fact]
    public void SetCurrentVersion_VersionNotInTemplate_ThrowsBusinessRuleViolationException()
    {
        var template = new Template(_projectId, "Invoice", "invoice");
        var version = new TemplateVersion(template.Id, 1, "key", TemplateFormat.Html, "author");
        template.AddVersion(version);

        var foreignVersionId = Guid.NewGuid();
        Action act = () => template.SetCurrentVersion(foreignVersionId);

        var ex = act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage($"*does not belong to template '{template.Id}'*");
        ex.Which.ErrorCode.Should().Be("VERSION_NOT_IN_TEMPLATE");
    }

    // ── Field Mappings ─────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceFieldMappings_ValidMappings_ReplacesCollection()
    {
        var template = new Template(_projectId, "Receipt", "receipt");
        var initial = new FieldMapping(template.Id, "name", "customer.name", "Name", true, 1);
        template.AddFieldMapping(initial);

        var replacement = new List<FieldMapping>
        {
            new(template.Id, "total", "invoice.total", "Total", true, 1),
            new(template.Id, "date", "invoice.date", "Date", false, 2)
        };

        template.ReplaceFieldMappings(replacement);

        template.FieldMappings.Should().HaveCount(2);
        template.FieldMappings.Select(m => m.Placeholder).Should().Contain(["total", "date"]);
    }

    [Fact]
    public void ReplaceFieldMappings_DuplicatePlaceholders_ThrowsBusinessRuleViolationException()
    {
        var template = new Template(_projectId, "Receipt", "receipt");
        var mappings = new List<FieldMapping>
        {
            new(template.Id, "customerName", "a", "A", true, 1),
            new(template.Id, "CUSTOMERNAME", "b", "B", false, 2) // Duplicate case-insensitive
        };

        Action act = () => template.ReplaceFieldMappings(mappings);

        var ex = act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Duplicate placeholders detected*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_PLACEHOLDER");
    }

    // ── Template Datasets ──────────────────────────────────────────────────────

    [Fact]
    public void AttachDataset_And_DetachDataset_ModifiesCollection()
    {
        var template = new Template(_projectId, "Order", "order");
        var dsId = Guid.NewGuid();

        template.AttachDataset(dsId, "items", 1);

        template.TemplateDatasets.Should().ContainSingle();
        template.TemplateDatasets.First().Alias.Should().Be("items");

        template.DetachDataset(dsId);

        template.TemplateDatasets.Should().BeEmpty();
    }

    [Fact]
    public void AttachDataset_DuplicateAlias_ThrowsBusinessRuleViolationException()
    {
        var template = new Template(_projectId, "Order", "order");
        template.AttachDataset(Guid.NewGuid(), "header", 1);

        Action act = () => template.AttachDataset(Guid.NewGuid(), "HEADER", 2);

        var ex = act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already assigned to template*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DATASET_ALIAS");
    }

    [Fact]
    public void ReplaceDatasets_DuplicateAliases_ThrowsBusinessRuleViolationException()
    {
        var template = new Template(_projectId, "Order", "order");
        var datasets = new List<TemplateDataset>
        {
            new(template.Id, Guid.NewGuid(), "customers", 1),
            new(template.Id, Guid.NewGuid(), "CUSTOMERS ", 2)
        };

        Action act = () => template.ReplaceDatasets(datasets);

        var ex = act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*aliases must be unique*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DATASET_ALIAS");
    }

    // ── Document Aggregate Invariants ──────────────────────────────────────────

    [Fact]
    public void Document_AddVersion_EnforcesEncapsulationAndDuplicateGuards()
    {
        var doc = new Document("DOC-001");
        var v1 = new DocumentVersion(doc.Id, 1, null, null, "First version", "admin");

        doc.AddVersion(v1);

        doc.Versions.Should().ContainSingle().Which.Should().BeSameAs(v1);

        var duplicateVersion = new DocumentVersion(doc.Id, 1, null, null, "Conflict version", "admin");
        Action actDuplicate = () => doc.AddVersion(duplicateVersion);

        var ex = actDuplicate.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already exists in document*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DOCUMENT_VERSION");
    }
}
