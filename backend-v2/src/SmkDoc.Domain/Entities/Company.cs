namespace SmkDoc.Domain.Entities;

public class Company : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    // Navigation properties
    public virtual ICollection<Project> Projects { get; private set; } = new List<Project>();

    private Company() { }

    public Company(string name)
    {
        Name = name;
        IsActive = true;
    }

    public void UpdateName(string newName)
    {
        Name = newName;
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
