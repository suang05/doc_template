using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;

public class ValidateTemplateHtmlUseCaseTests
{
    private readonly Mock<IPdfRenderer> _mockRenderer = new();

    private ValidateTemplateHtmlUseCase CreateSut() => new(_mockRenderer.Object);

    [Fact]
    public async Task ValidateHtmlAsync_WithValidTags_ShouldDetectFields()
    {
        // Arrange
        const string html = "<html><body><h1>Hello</h1><Field name=\"customerName\" label=\"Customer Name\" /><Field name=\"totalAmount\" label=\"Total\" /></body></html>";
        _mockRenderer.Setup(r => r.RenderHtmlToPdfAsync(html, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        // Act
        var result = await CreateSut().ValidateHtmlAsync(html);

        // Assert
        result.Valid.Should().BeTrue();
        result.Fields.Should().Contain(["customerName", "totalAmount"]);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateHtmlAsync_WithMissingNameAttribute_ShouldReportError()
    {
        // Arrange
        const string html = "<html><body><Field label=\"Missing Name\" /></body></html>";

        // Act
        var result = await CreateSut().ValidateHtmlAsync(html);

        // Assert
        result.Errors.Should().Contain(e => e.Contains("missing 'name' attribute"));
    }

    [Fact]
    public async Task ValidateHtmlAsync_WithEmptyHtml_ShouldReturnInvalid()
    {
        // Act
        var result = await CreateSut().ValidateHtmlAsync(string.Empty);

        // Assert
        result.Valid.Should().BeFalse();
        result.Errors.Should().Contain("HTML template cannot be empty.");
    }
}
