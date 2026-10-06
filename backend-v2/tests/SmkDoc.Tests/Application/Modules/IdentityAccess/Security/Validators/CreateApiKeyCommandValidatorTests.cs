using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Validators;

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

    [Fact]
    public async Task ValidateAsync_WithEmptyData_ShouldFail()
    {
        // Arrange
        var command = new CreateApiKeyCommand("", "");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateApiKeyCommand.Name));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateApiKeyCommand.CallerApp));
    }
}
