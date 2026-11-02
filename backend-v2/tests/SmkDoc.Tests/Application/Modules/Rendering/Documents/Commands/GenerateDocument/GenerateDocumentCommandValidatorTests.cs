using System.Text.Json;
using FluentAssertions;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Commands.GenerateDocument;

public sealed class GenerateDocumentCommandValidatorTests
{
    private readonly GenerateDocumentCommandValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WithValidPayload_ShouldPass()
    {
        // Arrange
        var dataJson = JsonDocument.Parse("{\"customerName\": \"John Doe\"}").RootElement;
        var command = new GenerateDocumentCommand("sale-contract", dataJson, "pdf", "REF-001", "Initial version", false);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_WithEmptySlug_ShouldFail(string slug)
    {
        // Arrange
        var dataJson = JsonDocument.Parse("{\"customerName\": \"John Doe\"}").RootElement;
        var command = new GenerateDocumentCommand(slug, dataJson, "pdf");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GenerateDocumentCommand.Slug));
    }

    [Theory]
    [InlineData("invalid_format")]
    [InlineData("txt")]
    [InlineData("")]
    public async Task ValidateAsync_WithInvalidOutputFormat_ShouldFail(string outputFormat)
    {
        // Arrange
        var dataJson = JsonDocument.Parse("{\"customerName\": \"John Doe\"}").RootElement;
        var command = new GenerateDocumentCommand("sale-contract", dataJson, outputFormat);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GenerateDocumentCommand.OutputFormat));
    }
}
