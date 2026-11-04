using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;

public sealed class RemoveUserCommandValidatorTests
{
    private readonly RemoveUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldPass()
    {
        var command = new RemoveUserCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenSelfRemoval_ShouldFail()
    {
        var userId = Guid.NewGuid();
        var command = new RemoveUserCommand(userId, userId);
        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("themselves"));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "9c5123d4-4f81-4b13-a417-64dfd48227b9", "UserId")]
    public void Validate_WhenGuidIsEmpty_ShouldFail(string userId, string currentUserId, string expectedProperty)
    {
        var command = new RemoveUserCommand(
            Guid.Parse(userId),
            Guid.Parse(currentUserId));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains(expectedProperty));
    }
}
