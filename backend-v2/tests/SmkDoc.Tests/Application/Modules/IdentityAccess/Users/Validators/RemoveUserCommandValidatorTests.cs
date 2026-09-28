using FluentAssertions;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Validators;

public class RemoveUserCommandValidatorTests
{
    private readonly RemoveUserCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var command = new RemoveUserCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_SelfRemoval_ShouldFail()
    {
        var userId = Guid.NewGuid();
        var command = new RemoveUserCommand(Guid.NewGuid(), userId, userId);
        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Cannot remove yourself"));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "e7d1b32f-7634-4b52-b88a-3e4b01e35d11", "9c5123d4-4f81-4b13-a417-64dfd48227b9", "ProjectId")]
    [InlineData("e7d1b32f-7634-4b52-b88a-3e4b01e35d11", "00000000-0000-0000-0000-000000000000", "9c5123d4-4f81-4b13-a417-64dfd48227b9", "UserId")]
    [InlineData("e7d1b32f-7634-4b52-b88a-3e4b01e35d11", "9c5123d4-4f81-4b13-a417-64dfd48227b9", "00000000-0000-0000-0000-000000000000", "CurrentUserId")]
    public void Validate_EmptyGuid_ShouldFail(string projectId, string userId, string currentUserId, string expectedProperty)
    {
        var command = new RemoveUserCommand(
            Guid.Parse(projectId),
            Guid.Parse(userId),
            Guid.Parse(currentUserId));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains(expectedProperty));
    }
}
