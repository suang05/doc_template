using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a version revision of a generated document.
/// </summary>
public sealed class DocumentVersion : BaseEntity
{
    public const int MaxChangeNoteLength = 500;
    public const int MaxCreatedByLength = 100;

    public Guid DocumentId { get; private set; }
    public int Version { get; private set; } = 1;
    public Guid? TemplateVersionId { get; private set; }
    public Guid? GenerationLogId { get; private set; }
    public string? ChangeNote { get; private set; }
    public string? CreatedBy { get; private set; }

    // For EF Core materialization
    private DocumentVersion() { }

    internal DocumentVersion(
        Guid? id,
        Guid documentId,
        int version,
        Guid? templateVersionId,
        Guid? generationLogId,
        string? changeNote,
        string? createdBy,
        DateTimeOffset now)
        : base(id, createdAt: now)
    {
        DocumentId = Guard.NotEmpty(documentId, nameof(DocumentId));
        Version = Guard.Positive(version, nameof(Version));

        if (changeNote is { Length: > MaxChangeNoteLength })
        {
            throw new DomainValidationException($"Change note must not exceed {MaxChangeNoteLength} characters.");
        }

        if (createdBy is { Length: > MaxCreatedByLength })
        {
            throw new DomainValidationException($"CreatedBy must not exceed {MaxCreatedByLength} characters.");
        }

        TemplateVersionId = templateVersionId;
        GenerationLogId = generationLogId;
        ChangeNote = changeNote?.Trim();
        CreatedBy = createdBy?.Trim();
    }

    public static DocumentVersion Create(
        Guid documentId,
        int version,
        Guid? templateVersionId,
        Guid? generationLogId,
        string? changeNote,
        string? createdBy,
        DateTimeOffset now) =>
        new(null, documentId, version, templateVersionId, generationLogId, changeNote, createdBy, now);
}
