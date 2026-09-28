using FluentAssertions;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Users.Validators;

public class InviteUserCommandValidatorTests
{
    private readonly InviteUserCommandValidator _validator = new();

    [Theory]
    [InlineData("Admin")]
    [InlineData("Developer")]
    [InlineData("Viewer")]
    [InlineData("admin")]
    [InlineData("DEVELOPER")]
    public void Validate_ValidRoles_ShouldPass(string role)
    {
        var command = new InviteUserCommand(Guid.NewGuid(), "user@test.com", "Pass@1234", "First", "Last", role);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Manager")]
    [InlineData("")]
    public void Validate_InvalidRole_ShouldFail(string role)
    {
        var command = new InviteUserCommand(Guid.NewGuid(), "user@test.com", "Pass@1234", "First", "Last", role);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Role");
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@nodomain.com")]
    [InlineData("")]
    public void Validate_InvalidEmail_ShouldFail(string email)
    {
        var command = new InviteUserCommand(Guid.NewGuid(), email, "Pass@1234", "First", "Last", "Viewer");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }
}
