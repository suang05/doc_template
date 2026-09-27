using SmkDoc.Domain.Enums;

namespace SmkDoc.Domain.Entities;

public class UserProjectRole
{
    private RoleType _role = RoleType.Viewer;

    public Guid UserId { get; init; }
    public Guid ProjectId { get; init; }
    public RoleType Role 
    { 
        get => _role; 
        init => _role = value; 
    }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual User? User { get; init; }
    public virtual Project? Project { get; init; }

    public UserProjectRole() { }

    public UserProjectRole(Guid userId, Guid projectId, RoleType role)
    {
        UserId = userId;
        ProjectId = projectId;
        _role = role;
    }

    public void UpdateRole(RoleType newRole)
    {
        _role = newRole;
    }
}

