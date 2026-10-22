using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;

public sealed class ValidateTemplateHtmlUseCaseTests
{
    private readonly IPdfRenderer _renderer = Substitute.For<IPdfRenderer>();

    private ValidateTemplateHtmlUseCase CreateSut() => new(_renderer);

    [Fact]
    public async Task ValidateHtmlAsync_WithValidTags_ShouldDetectFields()
    {
        // Arrange
        const string html = "<html><body><h1>Hello</h1><Field name=\"customerName\" label=\"Customer Name\" /><Field name=\"totalAmount\" label=\"Total\" /></body></html>";
        _renderer.RenderHtmlToPdfAsync(html, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([0x25, 0x50, 0x44, 0x46]);

        // Act
        TemplateValidationResultDto result = await CreateSut().ValidateHtmlAsync(html);

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
        TemplateValidationResultDto result = await CreateSut().ValidateHtmlAsync(html);

        // Assert
        result.Errors.Should().Contain(e => e.Contains("missing 'name' attribute"));
    }

    [Fact]
    public async Task ValidateHtmlAsync_WithEmptyHtml_ShouldReturnInvalid()
    {
        // Act
        TemplateValidationResultDto result = await CreateSut().ValidateHtmlAsync(string.Empty);

        // Assert
        result.Valid.Should().BeFalse();
        result.Errors.Should().Contain("HTML template cannot be empty.");
    }
}
