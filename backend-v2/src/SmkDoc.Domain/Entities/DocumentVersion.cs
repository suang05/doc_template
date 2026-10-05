using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a version revision of a generated document.
/// </summary>
public class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public int Version { get; private set; } = 1;
    public Guid? TemplateVersionId { get; private set; }
    public Guid? GenerationLogId { get; private set; }
    public string? ChangeNote { get; private set; }
    public string? CreatedBy { get; private set; }

    // Navigation properties
    public virtual Document? Document { get; private set; }
    public virtual TemplateVersion? TemplateVersion { get; private set; }
    public virtual GenerationLog? GenerationLog { get; private set; }

    // For EF Core materialization
    private DocumentVersion() { }

    public DocumentVersion(Guid documentId, int version, Guid? templateVersionId, Guid? generationLogId, string? changeNote, string? createdBy, Guid? id = null)
        : base(id)
    {
        if (documentId == Guid.Empty)
        {
            throw new DomainValidationException("DocumentId cannot be empty.");
        }

        if (version <= 0)
        {
            throw new DomainValidationException("Document version must be greater than zero.");
        }

        DocumentId = documentId;
        Version = version;
        TemplateVersionId = templateVersionId;
        GenerationLogId = generationLogId;
        ChangeNote = changeNote;
        CreatedBy = createdBy;
    }
}
