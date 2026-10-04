using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a business document tracked by the system.
/// </summary>
public class Document : BaseEntity
{
    public string DocumentRef { get; private set; } = string.Empty;
    public Guid? TemplateId { get; private set; }

    private readonly List<DocumentVersion> _versions = new();

    // Navigation properties
    public virtual Template? Template { get; private set; }
    public virtual IReadOnlyCollection<DocumentVersion> Versions => _versions.AsReadOnly();

    // For EF Core materialization
    private Document() { }

    public Document(string documentRef, Guid? templateId = null, Guid? id = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(documentRef))
        {
            throw new DomainValidationException("Document reference cannot be empty or whitespace.");
        }

        DocumentRef = documentRef.Trim();
        TemplateId = templateId;
    }

    public void AddVersion(DocumentVersion version)
    {
        if (version == null)
        {
            throw new DomainValidationException("DocumentVersion cannot be null.");
        }

        if (version.DocumentId != Id)
        {
            throw new DomainValidationException($"DocumentVersion document ID '{version.DocumentId}' does not match document ID '{Id}'.");
        }

        if (_versions.Any(v => v.Version == version.Version))
        {
            throw new BusinessRuleViolationException(
                $"Version {version.Version} already exists in document '{Id}'.",
                "DUPLICATE_DOCUMENT_VERSION");
        }

        _versions.Add(version);
        SetUpdated();
    }
}
