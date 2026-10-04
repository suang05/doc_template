using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a business project / tenant boundary.
/// </summary>
public class Project : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private readonly List<Template> _templates = new();
    private readonly List<ApiKey> _apiKeys = new();
    private readonly List<UserProjectRole> _userRoles = new();

    // Navigation properties
    public virtual Company? Company { get; private set; }
    public virtual IReadOnlyCollection<Template> Templates => _templates.AsReadOnly();
    public virtual IReadOnlyCollection<ApiKey> ApiKeys => _apiKeys.AsReadOnly();
    public virtual IReadOnlyCollection<UserProjectRole> UserRoles => _userRoles.AsReadOnly();

    // For EF Core materialization
    private Project() { }

    public Project(Guid companyId, string name, string slug, Guid? id = null)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainValidationException("CompanyId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Project name cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainValidationException("Project slug cannot be empty or whitespace.");
        }

        CompanyId = companyId;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        IsActive = true;
    }

    public void UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new DomainValidationException("Project name cannot be empty or whitespace.");
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
