using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public class CreateApiKeyCommandValidatorTests
{
    private readonly CreateApiKeyCommandValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WithValidData_ShouldPass()
    {
        // Arrange
        var command = new CreateApiKeyCommand("Test Key", "BillingService");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_WhenNameIsInvalid_ShouldFail(string? invalidName)
    {
        // Arrange
        var command = new CreateApiKeyCommand(invalidName!, "BillingService");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateApiKeyCommand.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_WhenCallerAppIsInvalid_ShouldFail(string? invalidCallerApp)
    {
        // Arrange
        var command = new CreateApiKeyCommand("Test Key", invalidCallerApp!);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateApiKeyCommand.CallerApp));
    }
}
