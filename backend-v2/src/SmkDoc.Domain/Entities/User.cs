using SmkDoc.Domain.Enums;

namespace SmkDoc.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public SystemRole SystemRole { get; private set; } = SystemRole.Member;

    // Navigation properties
    public virtual ICollection<UserProjectRole> ProjectRoles { get; private set; } = new List<UserProjectRole>();

    // For EF Core
    private User() { }

    public User(string email, string passwordHash, string firstName, string lastName, SystemRole? systemRole = null)
    {
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        SystemRole = systemRole ?? SystemRole.Member;
        IsActive = true;
    }

    public void AssignSystemRole(SystemRole role)
    {
        SystemRole = role;
        SetUpdated();
    }

    public bool CanAccessProject(Guid projectId)
    {
        if (SystemRole == SystemRole.SuperAdmin)
        {
            return true;
        }

        return ProjectRoles.Any(r => r.ProjectId == projectId);
    }


    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
        SetUpdated();
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }
}
