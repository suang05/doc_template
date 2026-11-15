using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a user's role assignment within a specific project.
/// Composite identity (UserId, ProjectId).
/// </summary>
public sealed class UserProjectRole : IConcurrencyAware, IEquatable<UserProjectRole>
{
    public UserId UserId { get; private set; }
    public ProjectId ProjectId { get; private set; }
    public object EntityId => UserId.Value;
    public RoleType Role { get; private set; } = RoleType.Viewer;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public long Version { get; private set; } = 1;

    // For EF Core materialization only
    private UserProjectRole() { }

    internal UserProjectRole(UserId userId, ProjectId projectId, RoleType role, DateTimeOffset now)
    {
        UserId = Guard.NotEmpty(userId, nameof(UserId));
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        Role = role ?? RoleType.Viewer;
        CreatedAt = now;
    }

    public static UserProjectRole Create(UserId userId, ProjectId projectId, RoleType role, DateTimeOffset now) =>
        new(userId, projectId, role, now);

    public void UpdateRole(RoleType newRole, DateTimeOffset now)
    {
        Role = newRole ?? RoleType.Viewer;
        UpdatedAt = now;
        Version++;
    }

    public bool Equals(UserProjectRole? other)
    {
        if (other is null) return false;
        return UserId == other.UserId && ProjectId == other.ProjectId;
    }

    public override bool Equals(object? obj) =>
        obj is UserProjectRole other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(UserId, ProjectId);
}
