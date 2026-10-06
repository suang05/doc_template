using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.InviteUser;

public class InviteUserCommandValidatorTests
{
    private readonly InviteUserCommandValidator _validator = new();

    [Theory]
    [InlineData("Admin")]
    [InlineData("Developer")]
    [InlineData("Viewer")]
    [InlineData("admin")]
    [InlineData("DEVELOPER")]
    public void Validate_WhenRoleIsValid_ShouldPass(string role)
    {
        var command = new InviteUserCommand(Guid.NewGuid(), "user@test.com", "Pass@1234", "First", "Last", role);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Manager")]
    [InlineData("")]
    public void Validate_WhenRoleIsInvalid_ShouldFail(string role)
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
    public void Validate_WhenEmailIsInvalid_ShouldFail(string email)
    {
        var command = new InviteUserCommand(Guid.NewGuid(), email, "Pass@1234", "First", "Last", "Viewer");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }
}
