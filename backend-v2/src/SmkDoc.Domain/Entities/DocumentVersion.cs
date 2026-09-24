namespace SmkDoc.Domain.Entities;

public class DocumentVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public int Version { get; set; } = 1;
    public Guid? TemplateVersionId { get; set; }
    public Guid? GenerationLogId { get; set; }
    public string? ChangeNote { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Document? Document { get; set; }
    public TemplateVersion? TemplateVersion { get; set; }
    public GenerationLog? GenerationLog { get; set; }
}
