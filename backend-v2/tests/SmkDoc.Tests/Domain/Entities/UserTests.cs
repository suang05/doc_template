using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class UserTests
{
    private readonly DateTimeOffset _initialTime = TestConstants.BaselineTime;

    [Fact]
    public void Register_WithValidParameters_InitializesActiveUser()
    {
        var user = User.Register(EmailAddress.Create("test@example.com"), "hash123", "John", "Doe", _initialTime);

        user.Id.Value.Should().NotBe(Guid.Empty);
        user.Email.Value.Should().Be("test@example.com");
        user.PasswordHash.Should().Be("hash123");
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.IsActive.Should().BeTrue();
        user.SystemRole.Should().Be(SystemRole.Member);
        user.CreatedAt.Should().Be(_initialTime);
        user.UpdatedAt.Should().BeNull();
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
    public void IsSuperAdmin_WhenRoleIsSuperAdmin_ReturnsTrue()
    {
        var user = User.Register(EmailAddress.Create("admin@example.com"), "hash123", "Super", "Admin", _initialTime, SystemRole.SuperAdmin);

        user.IsSuperAdmin.Should().BeTrue();
    }

    [Fact]
    public void IsSuperAdmin_WhenRoleIsMember_ReturnsFalse()
    {
        var user = User.Register(EmailAddress.Create("member@example.com"), "hash123", "Regular", "Member", _initialTime, SystemRole.Member);

        user.IsSuperAdmin.Should().BeFalse();
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

        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserDeactivatedDomainEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                UserId = user.Id,
                OccurredOn = deactTime
            });

        var reactTime = _initialTime.AddDays(2);
        user.Activate(reactTime);
        user.IsActive.Should().BeTrue();
        user.UpdatedAt.Should().Be(reactTime);
    }
}
