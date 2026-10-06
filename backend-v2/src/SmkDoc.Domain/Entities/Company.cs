using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a company / root tenant organization.
/// </summary>
public sealed class Company : BaseEntity
{
    public CompanyName Name { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private readonly List<Project> _projects = new();

    // Navigation properties
    public IReadOnlyCollection<Project> Projects => _projects.AsReadOnly();

    // For EF Core materialization only
    private Company() { }

    internal Company(Guid? id, CompanyName name, DateTimeOffset now)
        : base(id, createdAt: now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        IsActive = true;
    }

    public static Company Create(CompanyName name, DateTimeOffset now) =>
        new(null, name, now);

    public void UpdateName(CompanyName newName, DateTimeOffset now)
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
