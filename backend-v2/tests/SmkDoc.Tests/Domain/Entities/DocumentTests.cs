using FluentAssertions;
using SmkDoc.Domain.Entities;
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
}
