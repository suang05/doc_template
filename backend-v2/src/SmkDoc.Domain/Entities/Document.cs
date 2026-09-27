namespace SmkDoc.Domain.Entities;

public class Document : BaseEntity
{
    public string DocumentRef { get; private set; } = string.Empty;
    public Guid? TemplateId { get; private set; }

    // Navigation properties
    public virtual Template? Template { get; private set; }
    public virtual ICollection<DocumentVersion> Versions { get; private set; } = new List<DocumentVersion>();

    private Document() { }

    public Document(string documentRef, Guid? templateId = null)
    {
        DocumentRef = documentRef;
        TemplateId = templateId;
    }
}
