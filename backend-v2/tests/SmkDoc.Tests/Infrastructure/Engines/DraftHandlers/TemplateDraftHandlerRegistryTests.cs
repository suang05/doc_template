using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Services;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Infrastructure.Engines.DraftHandlers;

namespace SmkDoc.Tests.Infrastructure.Engines.DraftHandlers;

public sealed class TemplateDraftHandlerRegistryTests
{
    private readonly IDocxSecurityScanner _securityScanner = Substitute.For<IDocxSecurityScanner>();
    private readonly ITemplateScannerService _scannerService = Substitute.For<ITemplateScannerService>();

    private TemplateDraftHandlerRegistry CreateSut()
    {
        var handlers = new ITemplateDraftHandler[]
        {
            new DocxDraftHandler(_securityScanner, _scannerService),
            new HtmlDraftHandler(_scannerService),
            new XlsxDraftHandler(_scannerService)
        };

        return new TemplateDraftHandlerRegistry(handlers);
    }

    [Theory]
    [InlineData(".docx", typeof(DocxDraftHandler))]
    [InlineData("docx", typeof(DocxDraftHandler))]
    [InlineData(".html", typeof(HtmlDraftHandler))]
    [InlineData("html", typeof(HtmlDraftHandler))]
    [InlineData(".xlsx", typeof(XlsxDraftHandler))]
    [InlineData("xlsx", typeof(XlsxDraftHandler))]
    public void GetHandler_WithSupportedExtension_ShouldReturnMatchingHandler(string extension, Type expectedType)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var handler = sut.GetHandler(extension);

        // Assert
        handler.Should().NotBeNull();
        handler.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".pdf")]
    [InlineData(".txt")]
    [InlineData("")]
    public void GetHandler_WithUnsupportedExtension_ShouldThrowDomainValidationException(string extension)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.GetHandler(extension);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*Unsupported template file extension*");
    }

    [Fact]
    public async Task DocxDraftHandler_WhenSecurityScannerDetectsThreats_ShouldThrowDomainValidationException()
    {
        // Arrange
        _securityScanner.Scan(Arg.Any<Stream>())
            .Returns(new DocxScanResult(false, ["VBA macro project detected."]));

        var handler = new DocxDraftHandler(_securityScanner, _scannerService);
        using var stream = new MemoryStream();

        // Act
        var act = () => handler.ValidateSecurityAsync(stream);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*VBA macro project detected*");
    }

    [Fact]
    public async Task DocxDraftHandler_WhenSafe_ShouldCompleteSuccessfully()
    {
        // Arrange
        _securityScanner.Scan(Arg.Any<Stream>())
            .Returns(new DocxScanResult(true, []));

        var handler = new DocxDraftHandler(_securityScanner, _scannerService);
        using var stream = new MemoryStream();

        // Act
        var act = () => handler.ValidateSecurityAsync(stream);

        // Assert
        await act.Should().NotThrowAsync();
    }
}

