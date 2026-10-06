using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class DocumentTests
{
    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var templateId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        var document = Document.Create(DocumentReference.Create("DOC-2026-001"), templateId, now);

        document.Id.Should().NotBeEmpty();
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

        var document = Document.Create(docRef, null, now);

        document.DocumentRef.Should().Be(docRef);
        document.TemplateId.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullDocumentReference_ThrowsDomainValidationException()
    {
        var act = () => Document.Create(null!, null, TestConstants.BaselineTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*DocumentRef is required*");
    }

    [Fact]
    public void AddVersion_WithValidVersion_AddsToCollectionAndUpdatesTimestamp()
    {
        var createdTime = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);
        var updatedTime = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero);
        var document = Document.Create(DocumentReference.Create("DOC-001"), null, createdTime);

        var version = DocumentVersion.Create(document.Id, 1, null, null, "Initial draft", null, createdTime);
        document.AddVersion(version, updatedTime);

        document.Versions.Should().ContainSingle().Which.Should().BeSameAs(version);
        document.UpdatedAt.Should().Be(updatedTime);
    }

    [Fact]
    public void AddVersion_WithNullVersion_ThrowsDomainValidationException()
    {
        var now = TestConstants.BaselineTime;
        var document = Document.Create(DocumentReference.Create("DOC-001"), null, now);
        var act = () => document.AddVersion(null!, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*version is required*");
    }

    [Fact]
    public void AddVersion_WithMismatchedDocumentId_ThrowsDomainValidationException()
    {
        var now = TestConstants.BaselineTime;
        var document = Document.Create(DocumentReference.Create("DOC-001"), null, now);
        var otherDocumentId = Guid.NewGuid();
        var version = DocumentVersion.Create(otherDocumentId, 1, null, null, null, null, now);

        var act = () => document.AddVersion(version, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*does not match document ID*");
    }

    [Fact]
    public void AddVersion_WithDuplicateVersionNumber_ThrowsDuplicateDocumentVersionException()
    {
        var now = TestConstants.BaselineTime;
        var document = Document.Create(DocumentReference.Create("DOC-001"), null, now);
        var v1 = DocumentVersion.Create(document.Id, 1, null, null, null, null, now);
        document.AddVersion(v1, now);

        var duplicate = DocumentVersion.Create(document.Id, 1, null, null, "Duplicate v1", null, now);
        var act = () => document.AddVersion(duplicate, now);

        var ex = act.Should().Throw<DuplicateDocumentVersionException>();
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DOCUMENT_VERSION");
        ex.Which.Message.Should().Contain($"Version 1 already exists in document '{document.Id}'");
    }
}
