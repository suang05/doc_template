using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a user's role assignment within a specific project.
/// </summary>
public class UserProjectRole
{
    public Guid UserId { get; private set; }
    public Guid ProjectId { get; private set; }
    public RoleType Role { get; private set; } = RoleType.Viewer;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual User? User { get; private set; }
    public virtual Project? Project { get; private set; }

    // For EF Core materialization
    private UserProjectRole() { }

    public UserProjectRole(Guid userId, Guid projectId, RoleType role)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("UserId cannot be empty.");
        }

        if (projectId == Guid.Empty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        UserId = userId;
        ProjectId = projectId;
        Role = role;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateRole(RoleType newRole)
    {
        Role = newRole;
    }
}
