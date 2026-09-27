namespace SmkDoc.Domain.Entities;

public class Project : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    // Navigation properties
    public virtual Company? Company { get; private set; }
    public virtual ICollection<Template> Templates { get; private set; } = new List<Template>();
    public virtual ICollection<ApiKey> ApiKeys { get; private set; } = new List<ApiKey>();
    public virtual ICollection<UserProjectRole> UserRoles { get; private set; } = new List<UserProjectRole>();

    // For EF Core
    private Project() { }

    public Project(Guid companyId, string name, string slug)
    {
        CompanyId = companyId;
        Name = name;
        Slug = slug;
        IsActive = true;
    }

    public void UpdateName(string newName)
    {
        Name = newName;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }
}
