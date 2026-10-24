using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Aggregate root representing an authenticated system user.
/// </summary>
public sealed class User : BaseEntity<UserId>
{
    public EmailAddress Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public SystemRole SystemRole { get; private set; } = SystemRole.Member;

    // For EF Core materialization only
    private User() { }

    internal User(UserId? id, EmailAddress email, string passwordHash, string firstName, string? lastName,
        SystemRole? systemRole, DateTimeOffset now)
        : base(id.HasValue && !id.Value.IsEmpty ? id.Value : UserId.New(), createdAt: now)
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

    public bool IsSuperAdmin => SystemRole == SystemRole.SuperAdmin;

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
        AddDomainEvent(new UserDeactivatedDomainEvent(Id, now));
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive) return;
        IsActive = true;
        SetUpdated(now);
    }
}
