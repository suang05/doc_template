using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a user's role assignment within a specific project.
/// Composite identity (UserId, ProjectId).
/// </summary>
public sealed class UserProjectRole : IEquatable<UserProjectRole>
{
    public Guid UserId { get; private set; }
    public Guid ProjectId { get; private set; }
    public RoleType Role { get; private set; } = RoleType.Viewer;
    public DateTimeOffset CreatedAt { get; private set; }

    // For EF Core materialization only
    private UserProjectRole() { }

    internal UserProjectRole(Guid userId, Guid projectId, RoleType role, DateTimeOffset now)
    {
        UserId = Guard.NotEmpty(userId, nameof(UserId));
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        Role = role ?? RoleType.Viewer;
        CreatedAt = now;
    }

    public static UserProjectRole Create(Guid userId, Guid projectId, RoleType role, DateTimeOffset now) =>
        new(userId, projectId, role, now);

    public void UpdateRole(RoleType newRole)
    {
        Role = newRole ?? RoleType.Viewer;
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
