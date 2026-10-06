using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WithValidEmailAndPassword_ShouldPass()
    {
        // Arrange
        var command = new LoginCommand("admin@sammakorn.co.th", "Password123!");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Password123!")]
    [InlineData("not-an-email", "Password123!")]
    [InlineData("admin@sammakorn.co.th", "short")]
    public async Task ValidateAsync_WithInvalidInputs_ShouldFail(string email, string password)
    {
        // Arrange
        var command = new LoginCommand(email, password);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
