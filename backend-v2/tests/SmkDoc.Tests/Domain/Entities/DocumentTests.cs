using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class DocumentTests
{
    private readonly ProjectId _projectId = ProjectId.New();

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var templateId = TemplateId.New();
        var now = TestConstants.BaselineTime;

        var document = Document.Create(DocumentReference.Create("DOC-2026-001"), templateId, now, _projectId);

        document.Id.Value.Should().NotBeEmpty();
        document.ProjectId.Should().Be(_projectId);
        document.DocumentRef.Value.Should().Be("DOC-2026-001");
        document.TemplateId.Should().Be(templateId);
        document.CreatedAt.Should().Be(now);
        document.UpdatedAt.Should().BeNull();
        document.Versions.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithDocumentReference_InitializesCorrectly()
    {
        var docRef = DocumentReference.Create("REF-ABC");
        var now = TestConstants.BaselineTime;

        var document = Document.Create(docRef, null, now, _projectId);

        document.DocumentRef.Should().Be(docRef);
        document.ProjectId.Should().Be(_projectId);
        document.TemplateId.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullDocumentReference_ThrowsDomainValidationException()
    {
        var act = () => Document.Create(null!, null, TestConstants.BaselineTime, _projectId);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*DocumentRef is required*");
    }

    [Fact]
    public void Create_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => Document.Create(DocumentReference.Create("DOC-001"), null, TestConstants.BaselineTime, ProjectId.Empty);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public void AddVersion_WithValidVersion_AddsToCollectionAndUpdatesTimestamp()
    {
        var createdTime = TestConstants.BaselineTime;
        var updatedTime = TestConstants.BaselineTime.AddHours(1);
        var document = Document.Create(DocumentReference.Create("DOC-001"), null, createdTime, _projectId);

        var version = DocumentVersion.Create(document.Id, 1, null, null, "Initial draft", null, createdTime);
        document.AddVersion(version, updatedTime);

        document.Versions.Should().ContainSingle().Which.Should().BeSameAs(version);
        document.UpdatedAt.Should().Be(updatedTime);
    }

    [Fact]
    public void AddVersion_WithNullVersion_ThrowsDomainValidationException()
    {
        var now = TestConstants.BaselineTime;
        var document = Document.Create(DocumentReference.Create("DOC-001"), null, now, _projectId);
        var act = () => document.AddVersion(null!, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*version is required*");
    }

    [Fact]
    public void AddVersion_WithMismatchedDocumentId_ThrowsDomainValidationException()
    {
        var now = TestConstants.BaselineTime;
        var document = Document.Create(DocumentReference.Create("DOC-001"), null, now, _projectId);
        var otherDocumentId = DocumentId.New();
        var version = DocumentVersion.Create(otherDocumentId, 1, null, null, null, null, now);

        var act = () => document.AddVersion(version, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*does not match document ID*");
    }

    [Fact]
    public void AddVersion_WithDuplicateVersionNumber_ThrowsDuplicateDocumentVersionException()
    {
        var now = TestConstants.BaselineTime;
        var document = Document.Create(DocumentReference.Create("DOC-001"), null, now, _projectId);
        var v1 = DocumentVersion.Create(document.Id, 1, null, null, null, null, now);
        document.AddVersion(v1, now);

        var duplicate = DocumentVersion.Create(document.Id, 1, null, null, "Duplicate v1", null, now);
        var act = () => document.AddVersion(duplicate, now);

        var ex = act.Should().Throw<DuplicateDocumentVersionException>();
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DOCUMENT_VERSION");
        ex.Which.Message.Should().Contain($"Version 1 already exists in document '{document.Id}'");
    }

    [Fact]
    public void CreateVersion_WithAutomaticVersion_CalculatesNextVersionSequentially()
    {
        var now = TestConstants.BaselineTime;
        var document = Document.Create(DocumentReference.Create("DOC-AUTO"), null, now, _projectId);

        var v1 = document.CreateVersion(null, null, "Initial version", "author-1", now);
        var v2 = document.CreateVersion(null, null, "Second revision", "author-2", now.AddMinutes(5));

        v1.Version.Should().Be(1);
        v1.DocumentId.Should().Be(document.Id);
        v1.ChangeNote.Should().Be("Initial version");
        v1.CreatedBy.Should().Be("author-1");

        v2.Version.Should().Be(2);
        v2.DocumentId.Should().Be(document.Id);
        v2.ChangeNote.Should().Be("Second revision");
        v2.CreatedBy.Should().Be("author-2");

        document.Versions.Should().HaveCount(2);
        document.UpdatedAt.Should().Be(now.AddMinutes(5));

        document.DomainEvents.Should().HaveCount(2);
        document.DomainEvents.First().Should().BeOfType<DocumentVersionCreatedDomainEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                DocumentId = document.Id,
                ProjectId = document.ProjectId,
                Version = 1,
                TemplateVersionId = (Guid?)null,
                GenerationLogId = (Guid?)null,
                OccurredOn = now
            });
    }

    [Fact]
    public void CreateVersion_WithExplicitVersion_AddsVersionAndEnforcesInvariants()
    {
        var now = TestConstants.BaselineTime;
        var document = Document.Create(DocumentReference.Create("DOC-EXP"), null, now, _projectId);

        var v5 = document.CreateVersion(5, null, null, "Custom v5", null, now);

        v5.Version.Should().Be(5);
        document.Versions.Should().ContainSingle().Which.Should().BeSameAs(v5);

        // Next automatic version after v5 should be 6
        var vNext = document.CreateVersion(null, null, "Next auto", null, now.AddSeconds(1));
        vNext.Version.Should().Be(6);
    }

    [Fact]
    public void CreateVersion_WithDuplicateVersion_ThrowsDuplicateDocumentVersionException()
    {
        var now = TestConstants.BaselineTime;
        var document = Document.Create(DocumentReference.Create("DOC-DUP"), null, now, _projectId);
        document.CreateVersion(1, null, null, null, null, now);

        var act = () => document.CreateVersion(1, null, null, "Duplicate", null, now);

        var ex = act.Should().Throw<DuplicateDocumentVersionException>();
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DOCUMENT_VERSION");
    }
}
