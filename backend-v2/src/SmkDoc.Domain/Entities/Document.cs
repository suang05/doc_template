using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain aggregate root representing a tracked business document across versions.
/// </summary>
public sealed class Document : BaseEntity<DocumentId>, IMustHaveProject
{
    public ProjectId ProjectId { get; private set; }
    public DocumentReference DocumentRef { get; private set; } = null!;
    public TemplateId? TemplateId { get; private set; }

    private readonly List<DocumentVersion> _versions = new();

    public IReadOnlyCollection<DocumentVersion> Versions => _versions.AsReadOnly();

    // For EF Core materialization
    private Document() { }

    internal Document(
        DocumentId? id,
        DocumentReference documentRef,
        TemplateId? templateId,
        DateTimeOffset now,
        ProjectId projectId)
        : base(id.HasValue && !id.Value.IsEmpty ? id.Value : DocumentId.New(), createdAt: now)
    {
        DocumentRef = Guard.NotNull(documentRef, nameof(DocumentRef));
        TemplateId = templateId;
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
    }

    public static Document Create(
        DocumentReference documentRef,
        TemplateId? templateId,
        DateTimeOffset now,
        ProjectId projectId) =>
        new(null, documentRef, templateId, now, projectId);

    public DocumentVersion CreateVersion(
        Guid? templateVersionId,
        Guid? generationLogId,
        string? changeNote,
        string? createdBy,
        DateTimeOffset now)
    {
        var nextVersion = _versions.Count == 0 ? 1 : _versions.Max(v => v.Version) + 1;
        return CreateVersion(nextVersion, templateVersionId, generationLogId, changeNote, createdBy, now);
    }

    public DocumentVersion CreateVersion(
        int versionNumber,
        Guid? templateVersionId,
        Guid? generationLogId,
        string? changeNote,
        string? createdBy,
        DateTimeOffset now)
    {
        Guard.Positive(versionNumber, nameof(versionNumber));

        if (_versions.Any(v => v.Version == versionNumber))
        {
            throw new DuplicateDocumentVersionException(Id.Value, versionNumber);
        }

        var version = DocumentVersion.Create(Id, versionNumber, templateVersionId, generationLogId, changeNote, createdBy, now);
        _versions.Add(version);
        SetUpdated(now);
        return version;
    }

    public void AddVersion(DocumentVersion version, DateTimeOffset now)
    {
        Guard.NotNull(version, nameof(version));

        if (version.DocumentId != Id)
        {
            throw new DomainValidationException(
                $"DocumentVersion document ID '{version.DocumentId}' does not match document ID '{Id}'.");
        }

        if (_versions.Any(v => v.Version == version.Version))
        {
            throw new DuplicateDocumentVersionException(Id.Value, version.Version);
        }

        _versions.Add(version);
        SetUpdated(now);
    }
}
