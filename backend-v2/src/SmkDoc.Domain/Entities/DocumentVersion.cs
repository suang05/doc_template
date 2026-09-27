namespace SmkDoc.Domain.Entities;

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

    private DocumentVersion() { }

    public DocumentVersion(Guid documentId, int version, Guid? templateVersionId, Guid? generationLogId, string? changeNote, string? createdBy)
    {
        DocumentId = documentId;
        Version = version;
        TemplateVersionId = templateVersionId;
        GenerationLogId = generationLogId;
        ChangeNote = changeNote;
        CreatedBy = createdBy;
    }
}
