using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class UserTests
{
    private readonly DateTimeOffset _initialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_WithValidParameters_InitializesActiveUser()
    {
        var user = User.Register(EmailAddress.Create("test@example.com"), "hash123", "John", "Doe", _initialTime);

        user.Id.Should().NotBe(Guid.Empty);
        user.Email.Value.Should().Be("test@example.com");
        user.PasswordHash.Should().Be("hash123");
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.IsActive.Should().BeTrue();
        user.SystemRole.Should().Be(SystemRole.Member);
        user.CreatedAt.Should().Be(_initialTime);
        user.UpdatedAt.Should().BeNull();
        user.ProjectRoles.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Register_WithInvalidEmail_ThrowsDomainValidationException(string? invalidEmail)
    {
        var act = () => User.Register(EmailAddress.Create(invalidEmail!), "hash123", "John", "Doe", _initialTime);

        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_WithInvalidPasswordHash_ThrowsDomainValidationException(string? invalidHash)
    {
        var act = () => User.Register(EmailAddress.Create("test@example.com"), invalidHash!, "John", "Doe", _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*PasswordHash*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_WithInvalidFirstName_ThrowsDomainValidationException(string? invalidName)
    {
        var act = () => User.Register(EmailAddress.Create("test@example.com"), "hash123", invalidName!, "Doe", _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*FirstName*");
    }

    [Fact]
    public void AssignSystemRole_UpdatesRoleAndAuditTimestamp()
    {
        var user = User.Register(EmailAddress.Create("test@example.com"), "hash123", "John", "Doe", _initialTime);
        var updateTime = _initialTime.AddHours(1);

        user.AssignSystemRole(SystemRole.SuperAdmin, updateTime);

        user.SystemRole.Should().Be(SystemRole.SuperAdmin);
        user.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void CanAccessProject_SuperAdmin_ReturnsTrueForAnyProject()
    {
        var user = User.Register(EmailAddress.Create("admin@example.com"), "hash123", "Super", "Admin", _initialTime, SystemRole.SuperAdmin);
        var arbitraryProjectId = Guid.NewGuid();

        user.CanAccessProject(arbitraryProjectId).Should().BeTrue();
    }

    [Fact]
    public void CanAccessProject_Member_ReturnsTrueOnlyIfRoleAssigned()
    {
        var user = User.Register(EmailAddress.Create("member@example.com"), "hash123", "Regular", "Member", _initialTime);
        var assignedProject = Guid.NewGuid();
        var unassignedProject = Guid.NewGuid();

        user.AssignProjectRole(assignedProject, RoleType.Developer, _initialTime);

        user.CanAccessProject(assignedProject).Should().BeTrue();
        user.CanAccessProject(unassignedProject).Should().BeFalse();
    }

    [Fact]
    public void AssignProjectRole_WhenNew_AddsToRoles()
    {
        var user = User.Register(EmailAddress.Create("member@example.com"), "hash123", "Regular", "Member", _initialTime);
        var projectId = Guid.NewGuid();
        var assignTime = _initialTime.AddHours(2);

        user.AssignProjectRole(projectId, RoleType.Admin, assignTime);

        user.ProjectRoles.Should().ContainSingle()
            .Which.Should().Match<UserProjectRole>(r => r.ProjectId == projectId && r.Role == RoleType.Admin);
        user.UpdatedAt.Should().Be(assignTime);
    }

    [Fact]
    public void AssignProjectRole_WhenExisting_UpdatesRoleInPlace()
    {
        var user = User.Register(EmailAddress.Create("member@example.com"), "hash123", "Regular", "Member", _initialTime);
        var projectId = Guid.NewGuid();
        user.AssignProjectRole(projectId, RoleType.Viewer, _initialTime);

        var updateTime = _initialTime.AddHours(3);
        user.AssignProjectRole(projectId, RoleType.Developer, updateTime);

        user.ProjectRoles.Should().ContainSingle()
            .Which.Role.Should().Be(RoleType.Developer);
        user.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void RemoveProjectRole_WhenPresent_RemovesAndSetsUpdated()
    {
        var user = User.Register(EmailAddress.Create("member@example.com"), "hash123", "Regular", "Member", _initialTime);
        var projectId = Guid.NewGuid();
        user.AssignProjectRole(projectId, RoleType.Viewer, _initialTime);

        var removeTime = _initialTime.AddHours(4);
        user.RemoveProjectRole(projectId, removeTime);

        user.ProjectRoles.Should().BeEmpty();
        user.UpdatedAt.Should().Be(removeTime);
    }

    [Fact]
    public void UpdateProfile_WithValidData_UpdatesAndSetsUpdated()
    {
        var user = User.Register(EmailAddress.Create("member@example.com"), "hash123", "OldFirst", "OldLast", _initialTime);
        var updateTime = _initialTime.AddHours(1);

        user.UpdateProfile("NewFirst", "NewLast", updateTime);

        user.FirstName.Should().Be("NewFirst");
        user.LastName.Should().Be("NewLast");
        user.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void UpdatePassword_WithValidHash_UpdatesAndSetsUpdated()
    {
        var user = User.Register(EmailAddress.Create("member@example.com"), "hash123", "John", "Doe", _initialTime);
        var updateTime = _initialTime.AddHours(1);

        user.UpdatePassword("new_hashed_secret", updateTime);

        user.PasswordHash.Should().Be("new_hashed_secret");
        user.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void DeactivateAndActivate_Lifecycle_BehavesCorrectly()
    {
        var user = User.Register(EmailAddress.Create("member@example.com"), "hash123", "John", "Doe", _initialTime);
        var deactTime = _initialTime.AddDays(1);

        user.Deactivate(deactTime);
        user.IsActive.Should().BeFalse();
        user.UpdatedAt.Should().Be(deactTime);

        var reactTime = _initialTime.AddDays(2);
        user.Activate(reactTime);
        user.IsActive.Should().BeTrue();
        user.UpdatedAt.Should().Be(reactTime);
    }
}
