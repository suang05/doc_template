using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Aggregate root representing a business project / tenant boundary.
/// </summary>
public sealed class Project : AggregateRoot<ProjectId>
{
    public CompanyId CompanyId { get; private set; }
    public ProjectName Name { get; private set; } = null!;
    public ProjectSlug Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public string? AllowedEgressCidrs { get; private set; }

    // For EF Core materialization only
    private Project() { }

    internal Project(ProjectId? id, CompanyId companyId, ProjectName name, ProjectSlug slug, DateTimeOffset now)
        : base(id ?? ProjectId.New(), createdAt: now)
    {
        CompanyId = Guard.NotEmpty(companyId, nameof(CompanyId));
        Name = Guard.NotNull(name, nameof(Name));
        Slug = Guard.NotNull(slug, nameof(Slug));
        IsActive = true;
        AddDomainEvent(new Events.ProjectProvisionedDomainEvent(Id, CompanyId, Slug.Value, now));
    }

    public static Project Create(CompanyId companyId, ProjectName name, ProjectSlug slug, DateTimeOffset now) =>
        new(null, companyId, name, slug, now);

    public void UpdateName(ProjectName newName, DateTimeOffset now)
    {
        Guard.NotNull(newName, nameof(newName));
        if (Name == newName) return;

        Name = newName;
        SetUpdated(now);
    }

    public void SetAllowedEgressCidrs(string? cidrs, DateTimeOffset now)
    {
        var normalized = string.IsNullOrWhiteSpace(cidrs) ? null : cidrs.Trim();
        if (AllowedEgressCidrs == normalized) return;

        AllowedEgressCidrs = normalized;
        SetUpdated(now);
    }

    public IReadOnlyList<string> GetAllowedEgressCidrList() =>
        string.IsNullOrWhiteSpace(AllowedEgressCidrs)
            ? []
            : AllowedEgressCidrs.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Activate(DateTimeOffset now)
    {
        if (IsActive) return;
        IsActive = true;
        SetUpdated(now);
        AddDomainEvent(new Events.ProjectActivatedDomainEvent(Id, now));
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated(now);
        AddDomainEvent(new Events.ProjectDeactivatedDomainEvent(Id, now));
    }

    public void RecordOwnershipTransfer(DateTimeOffset now) =>
        SetUpdated(now);
}
