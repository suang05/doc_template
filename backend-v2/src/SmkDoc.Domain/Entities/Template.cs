namespace SmkDoc.Domain.Entities;

public class Template
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CurrentVersionId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Project? Project { get; set; }
    public TemplateVersion? CurrentVersion { get; set; }
    public ICollection<FieldMapping> FieldMappings { get; set; } = new List<FieldMapping>();
    public ICollection<TemplateVersion> Versions { get; set; } = new List<TemplateVersion>();
    public ICollection<TemplateDataset> TemplateDatasets { get; set; } = new List<TemplateDataset>();
}
