using FluentAssertions;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WhenTokenIsNullOrWhitespace_FailsValidation(string? token)
    {
        var command = new RefreshTokenCommand(token!);
        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RefreshTokenCommand.RefreshToken));
    }

    [Fact]
    public void Validate_WhenTokenExceedsMaxLength_FailsValidation()
    {
        var longToken = new string('a', 257);
        var command = new RefreshTokenCommand(longToken);
        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("exceed 256 characters"));
    }

    [Fact]
    public void Validate_WhenTokenIsValid_PassesValidation()
    {
        var command = new RefreshTokenCommand("valid-refresh-token-string");
        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
