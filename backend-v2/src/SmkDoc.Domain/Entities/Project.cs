using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Aggregate root representing a business project / tenant boundary.
/// </summary>
public sealed class Project : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public ProjectName Name { get; private set; } = null!;
    public TemplateSlug Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private readonly List<Template> _templates = new();
    private readonly List<ApiKey> _apiKeys = new();
    private readonly List<UserProjectRole> _userRoles = new();

    // Child collections owned by this project aggregate boundary
    public IReadOnlyCollection<Template> Templates => _templates.AsReadOnly();
    public IReadOnlyCollection<ApiKey> ApiKeys => _apiKeys.AsReadOnly();
    public IReadOnlyCollection<UserProjectRole> UserRoles => _userRoles.AsReadOnly();

    // For EF Core materialization only
    private Project() { }

    internal Project(Guid? id, Guid companyId, ProjectName name, TemplateSlug slug, DateTimeOffset now)
        : base(id, createdAt: now)
    {
        CompanyId = Guard.NotEmpty(companyId, nameof(CompanyId));
        Name = Guard.NotNull(name, nameof(Name));
        Slug = Guard.NotNull(slug, nameof(Slug));
        IsActive = true;
    }

    public static Project Create(Guid companyId, ProjectName name, TemplateSlug slug, DateTimeOffset now) =>
        new(null, companyId, name, slug, now);

    public void UpdateName(ProjectName newName, DateTimeOffset now)
    {
        Guard.NotNull(newName, nameof(newName));
        if (Name == newName) return;

        Name = newName;
        SetUpdated(now);
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive) return;
        IsActive = true;
        SetUpdated(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated(now);
    }
}
