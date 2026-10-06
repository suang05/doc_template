using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class DocumentVersionTests
{
    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var documentId = Guid.NewGuid();
        var templateVersionId = Guid.NewGuid();
        var generationLogId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 6, 14, 30, 0, TimeSpan.Zero);

        var version = DocumentVersion.Create(
            documentId,
            1,
            templateVersionId,
            generationLogId,
            "Signed by customer",
            "app-portal",
            now);

        version.Id.Should().NotBeEmpty();
        version.DocumentId.Should().Be(documentId);
        version.Version.Should().Be(1);
        version.TemplateVersionId.Should().Be(templateVersionId);
        version.GenerationLogId.Should().Be(generationLogId);
        version.ChangeNote.Should().Be("Signed by customer");
        version.CreatedBy.Should().Be("app-portal");
        version.CreatedAt.Should().Be(now);
        version.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyDocumentId_ThrowsDomainValidationException()
    {
        var now = DateTimeOffset.UtcNow;
        var act = () => DocumentVersion.Create(Guid.Empty, 1, null, null, null, null, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*DocumentId cannot be empty*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithNonPositiveVersion_ThrowsDomainValidationException(int invalidVersion)
    {
        var now = DateTimeOffset.UtcNow;
        var act = () => DocumentVersion.Create(Guid.NewGuid(), invalidVersion, null, null, null, null, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*must be greater than zero*");
    }

    [Fact]
    public void Create_WithChangeNoteExceedingMaxLength_ThrowsDomainValidationException()
    {
        var now = DateTimeOffset.UtcNow;
        var longNote = new string('N', DocumentVersion.MaxChangeNoteLength + 1);
        var act = () => DocumentVersion.Create(Guid.NewGuid(), 1, null, null, longNote, null, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {DocumentVersion.MaxChangeNoteLength} characters*");
    }

    [Fact]
    public void Create_WithCreatedByExceedingMaxLength_ThrowsDomainValidationException()
    {
        var now = DateTimeOffset.UtcNow;
        var longUser = new string('U', DocumentVersion.MaxCreatedByLength + 1);
        var act = () => DocumentVersion.Create(Guid.NewGuid(), 1, null, null, null, longUser, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*must not exceed {DocumentVersion.MaxCreatedByLength} characters*");
    }
}
