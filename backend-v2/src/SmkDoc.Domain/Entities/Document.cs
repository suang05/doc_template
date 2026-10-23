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
