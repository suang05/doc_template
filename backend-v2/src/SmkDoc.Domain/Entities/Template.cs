using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Domain.Entities;

public class Template : BaseEntity, IMustHaveProject
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Category { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? CurrentVersionId { get; private set; }
    public Guid ProjectId { get; private set; }

    // Navigation properties
    public virtual Project? Project { get; private set; }
    public virtual TemplateVersion? CurrentVersion { get; private set; }
    public virtual ICollection<FieldMapping> FieldMappings { get; private set; } = new List<FieldMapping>();
    public virtual ICollection<TemplateVersion> Versions { get; private set; } = new List<TemplateVersion>();
    public virtual ICollection<TemplateDataset> TemplateDatasets { get; private set; } = new List<TemplateDataset>();

    private Template() { }

    public Template(Guid projectId, string name, string slug, string? category = null)
    {
        ProjectId = projectId;
        Name = name;
        Slug = slug;
        Category = category;
        IsActive = true;
    }

    public void SetCurrentVersion(Guid versionId)
    {
        CurrentVersionId = versionId;
        SetUpdated();
    }

    public void UpdateDetails(string name, string? category)
    {
        Name = name;
        Category = category;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }
}
