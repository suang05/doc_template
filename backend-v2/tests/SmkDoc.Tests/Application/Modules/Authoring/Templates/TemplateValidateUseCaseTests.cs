using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates;

public class TemplateValidateUseCaseTests
{
    private readonly Mock<IPdfRenderer> _mockRenderer = new();

    [Fact]
    public async Task ValidateHtmlAsync_WithValidTags_ShouldDetectFields()
    {
        // Arrange
        var html = "<html><body><h1>Hello</h1><Field name=\"customerName\" label=\"Customer Name\" /><Field name=\"totalAmount\" label=\"Total\" /></body></html>";
        _mockRenderer.Setup(r => r.RenderHtmlToPdfAsync(html, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var useCase = new TemplateValidateUseCase(_mockRenderer.Object);

        // Act
        var result = await useCase.ValidateHtmlAsync(html);

        // Assert
        result.Valid.Should().BeTrue();
        result.Fields.Should().Contain("customerName");
        result.Fields.Should().Contain("totalAmount");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateHtmlAsync_WithMissingNameAttribute_ShouldReportError()
    {
        // Arrange
        var html = "<html><body><Field label=\"Missing Name\" /></body></html>";
        var useCase = new TemplateValidateUseCase(_mockRenderer.Object);

        // Act
        var result = await useCase.ValidateHtmlAsync(html);

        // Assert
        result.Errors.Should().Contain(e => e.Contains("missing 'name' attribute"));
    }

    [Fact]
    public async Task ValidateHtmlAsync_WithEmptyHtml_ShouldReturnInvalid()
    {
        // Arrange
        var useCase = new TemplateValidateUseCase(_mockRenderer.Object);

        // Act
        var result = await useCase.ValidateHtmlAsync("");

        // Assert
        result.Valid.Should().BeFalse();
        result.Errors.Should().Contain("HTML template cannot be empty.");
    }
}
