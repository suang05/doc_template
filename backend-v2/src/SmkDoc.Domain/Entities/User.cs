using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Aggregate root representing an authenticated system user.
/// </summary>
public sealed class User : BaseEntity
{
    public EmailAddress Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public SystemRole SystemRole { get; private set; } = SystemRole.Member;

    private readonly List<UserProjectRole> _projectRoles = new();

    // Navigation properties (child collection within User aggregate)
    public IReadOnlyCollection<UserProjectRole> ProjectRoles => _projectRoles.AsReadOnly();

    // For EF Core materialization only
    private User() { }

    internal User(Guid? id, EmailAddress email, string passwordHash, string firstName, string? lastName,
        SystemRole? systemRole, DateTimeOffset now)
        : base(id, createdAt: now)
    {
        Email = Guard.NotNull(email, nameof(Email));
        PasswordHash = Guard.NotBlank(passwordHash, nameof(PasswordHash), 500);
        FirstName = Guard.NotBlank(firstName, nameof(FirstName), 100);
        LastName = (lastName ?? string.Empty).Trim();
        SystemRole = systemRole ?? SystemRole.Member;
        IsActive = true;
    }

    public static User Register(EmailAddress email, string passwordHash, string firstName, string? lastName,
        DateTimeOffset now, SystemRole? systemRole = null) =>
        new(null, email, passwordHash, firstName, lastName, systemRole, now);

    public void AssignSystemRole(SystemRole role, DateTimeOffset now)
    {
        SystemRole = role ?? SystemRole.Member;
        SetUpdated(now);
    }

    public bool CanAccessProject(Guid projectId)
    {
        if (SystemRole == SystemRole.SuperAdmin)
        {
            return true;
        }

        return ProjectRoles.Any(r => r.ProjectId == projectId);
    }

    public void UpdateProfile(string firstName, string lastName, DateTimeOffset now)
    {
        FirstName = Guard.NotBlank(firstName, nameof(FirstName), 100);
        LastName = (lastName ?? string.Empty).Trim();
        SetUpdated(now);
    }

    public void UpdatePassword(string newPasswordHash, DateTimeOffset now)
    {
        PasswordHash = Guard.NotBlank(newPasswordHash, nameof(PasswordHash), 500);
        SetUpdated(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated(now);
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive) return;
        IsActive = true;
        SetUpdated(now);
    }

    public void AssignProjectRole(UserProjectRole role, DateTimeOffset now)
    {
        Guard.NotNull(role, nameof(role));
        if (role.UserId != Id)
        {
            throw new DomainValidationException($"Role user ID '{role.UserId}' does not match user ID '{Id}'.");
        }

        var existing = _projectRoles.FirstOrDefault(r => r.ProjectId == role.ProjectId);
        if (existing != null)
        {
            existing.UpdateRole(role.Role);
        }
        else
        {
            _projectRoles.Add(role);
        }

        SetUpdated(now);
    }

    public void AssignProjectRole(Guid projectId, RoleType role, DateTimeOffset now)
    {
        Guard.NotEmpty(projectId, nameof(projectId));
        var existing = _projectRoles.FirstOrDefault(r => r.ProjectId == projectId);
        if (existing != null)
        {
            existing.UpdateRole(role);
        }
        else
        {
            _projectRoles.Add(UserProjectRole.Create(Id, projectId, role, now));
        }

        SetUpdated(now);
    }

    public void RemoveProjectRole(Guid projectId, DateTimeOffset now)
    {
        var existing = _projectRoles.FirstOrDefault(r => r.ProjectId == projectId);
        if (existing != null)
        {
            _projectRoles.Remove(existing);
            SetUpdated(now);
        }
    }
}
