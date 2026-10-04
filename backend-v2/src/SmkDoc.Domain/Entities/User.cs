using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing an authenticated system user.
/// </summary>
public class User : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public SystemRole SystemRole { get; private set; } = SystemRole.Member;

    private readonly List<UserProjectRole> _projectRoles = new();

    // Navigation properties
    public virtual IReadOnlyCollection<UserProjectRole> ProjectRoles => _projectRoles.AsReadOnly();

    // For EF Core materialization
    private User() { }

    public User(string email, string passwordHash, string? firstName, string? lastName, SystemRole? systemRole = null, Guid? id = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainValidationException("Email cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainValidationException("Password hash cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new DomainValidationException("FirstName cannot be empty or whitespace.");
        }

        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        FirstName = firstName.Trim();
        LastName = (lastName ?? string.Empty).Trim();
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
        FirstName = (firstName ?? string.Empty).Trim();
        LastName = (lastName ?? string.Empty).Trim();
        SetUpdated();
    }

    public void UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new DomainValidationException("Password hash cannot be empty or whitespace.");
        }

        PasswordHash = newPasswordHash;
        SetUpdated();
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        SetUpdated();
    }

    public void AssignProjectRole(UserProjectRole role)
    {
        if (role == null)
        {
            throw new DomainValidationException("UserProjectRole cannot be null.");
        }

        if (role.UserId != Id)
        {
            throw new DomainValidationException($"Role user ID '{role.UserId}' does not match user ID '{Id}'.");
        }

        var existing = _projectRoles.FirstOrDefault(r => r.ProjectId == role.ProjectId);
        if (existing != null)
        {
            _projectRoles.Remove(existing);
        }

        _projectRoles.Add(role);
        SetUpdated();
    }

    public void RemoveProjectRole(Guid projectId)
    {
        var existing = _projectRoles.FirstOrDefault(r => r.ProjectId == projectId);
        if (existing != null)
        {
            _projectRoles.Remove(existing);
            SetUpdated();
        }
    }
}
