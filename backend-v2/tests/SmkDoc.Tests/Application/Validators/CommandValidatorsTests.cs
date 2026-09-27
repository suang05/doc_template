using System.Text.Json;
using FluentAssertions;
using SmkDoc.Application.DTOs.Documents;
using SmkDoc.Application.DTOs.Security;
using SmkDoc.Application.DTOs.Templates;
using SmkDoc.Application.DTOs.Users;
using SmkDoc.Application.Validators.Documents;
using SmkDoc.Application.Validators.Security;
using SmkDoc.Application.Validators.Templates;
using Xunit;

namespace SmkDoc.Tests.Application.Validators;

public class CommandValidatorsTests
{
    [Fact]
    public async Task GenerateDocumentCommandValidator_WithValidPayload_ShouldPass()
    {
        // Arrange
        var validator = new GenerateDocumentCommandValidator();
        var dataJson = JsonDocument.Parse("{\"customerName\": \"John Doe\"}").RootElement;
        var command = new GenerateDocumentCommand(dataJson, "pdf", "REF-001", "Initial version", false);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid_format")]
    [InlineData("txt")]
    [InlineData("")]
    public async Task GenerateDocumentCommandValidator_WithInvalidOutputFormat_ShouldFail(string outputFormat)
    {
        // Arrange
        var validator = new GenerateDocumentCommandValidator();
        var dataJson = JsonDocument.Parse("{\"customerName\": \"John Doe\"}").RootElement;
        var command = new GenerateDocumentCommand(dataJson, outputFormat);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GenerateDocumentCommand.Output));
    }

    [Fact]
    public async Task CreateTemplateCommandValidator_WithValidSlug_ShouldPass()
    {
        // Arrange
        var validator = new CreateTemplateCommandValidator();
        var command = new CreateTemplateCommand("Receipt Template", "receipt-template-v1", "Receipts", "<html></html>");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Invalid Slug With Spaces")]
    [InlineData("UPPERCASE-SLUG")]
    [InlineData("slug_with_underscores")]
    [InlineData("-leading-dash")]
    [InlineData("trailing-dash-")]
    public async Task CreateTemplateCommandValidator_WithInvalidSlug_ShouldFail(string invalidSlug)
    {
        // Arrange
        var validator = new CreateTemplateCommandValidator();
        var command = new CreateTemplateCommand("Receipt Template", invalidSlug, "Receipts", "<html></html>");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTemplateCommand.Slug));
    }

    [Fact]
    public async Task LoginCommandValidator_WithValidEmailAndPassword_ShouldPass()
    {
        // Arrange
        var validator = new LoginCommandValidator();
        var command = new LoginCommand("admin@sammakorn.co.th", "Password123!");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Password123!")]
    [InlineData("not-an-email", "Password123!")]
    [InlineData("admin@sammakorn.co.th", "short")]
    public async Task LoginCommandValidator_WithInvalidInputs_ShouldFail(string email, string password)
    {
        // Arrange
        var validator = new LoginCommandValidator();
        var command = new LoginCommand(email, password);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task InviteUserCommandValidator_WithValidData_ShouldPass()
    {
        // Arrange
        var validator = new InviteUserCommandValidator();
        var command = new InviteUserCommand("user@sammakorn.co.th", "Pass12345", "Somchai", "Jaidee", "Editor");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task InviteUserCommandValidator_WithInvalidRole_ShouldFail()
    {
        // Arrange
        var validator = new InviteUserCommandValidator();
        var command = new InviteUserCommand("user@sammakorn.co.th", "Pass12345", "Somchai", "Jaidee", "SuperAdmin");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(InviteUserCommand.Role));
    }
}
