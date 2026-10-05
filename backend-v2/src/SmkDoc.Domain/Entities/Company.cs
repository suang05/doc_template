using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a company / root tenant organization.
/// </summary>
public class Company : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private readonly List<Project> _projects = new();

    // Navigation properties
    public virtual IReadOnlyCollection<Project> Projects => _projects.AsReadOnly();

    // For EF Core materialization
    private Company() { }

    public Company(string name, Guid? id = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Company name cannot be empty or whitespace.");
        }

        Name = name.Trim();
        IsActive = true;
    }

    public void UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new DomainValidationException("Company name cannot be empty or whitespace.");
        }

        Name = newName.Trim();
        SetUpdated();
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        SetUpdated();
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated();
    }
}
