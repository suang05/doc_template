using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain aggregate root representing a tracked business document across versions.
/// </summary>
public sealed class Document : BaseEntity
{
    public DocumentReference DocumentRef { get; private set; } = null!;
    public Guid? TemplateId { get; private set; }

    private readonly List<DocumentVersion> _versions = new();

    public IReadOnlyCollection<DocumentVersion> Versions => _versions.AsReadOnly();

    // For EF Core materialization
    private Document() { }

    internal Document(
        Guid? id,
        DocumentReference documentRef,
        Guid? templateId,
        DateTimeOffset now)
        : base(id, createdAt: now)
    {
        DocumentRef = Guard.NotNull(documentRef, nameof(DocumentRef));
        TemplateId = templateId;
    }

    public static Document Create(
        DocumentReference documentRef,
        Guid? templateId,
        DateTimeOffset now) =>
        new(null, documentRef, templateId, now);

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
            throw new DuplicateDocumentVersionException(Id, version.Version);
        }

        _versions.Add(version);
        SetUpdated(now);
    }
}
