using System.Text;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates;

public class HtmlStudioUseCaseTests
{
    private readonly Mock<IRepository<Template>>        _mockTemplateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo  = new();
    private readonly Mock<IStorageService>              _mockStorage      = new();
    private readonly Mock<IRenderEngine>                _mockHtmlEngine   = new();
    private readonly Mock<ITemplateScannerService>      _mockScanner      = new();

    public HtmlStudioUseCaseTests()
    {
        _mockHtmlEngine.SetupGet(e => e.EngineType).Returns(RenderEngineType.Html);
    }

    [Fact]
    public async Task PreviewHtmlBufferAsync_ShouldRenderInMemory_WithoutTouchingStorageOrDatabase()
    {
        // Arrange
        byte[] expectedPdf = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Stream");
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPdf);

        var useCase = new HtmlStudioUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockStorage.Object,
            new[] { _mockHtmlEngine.Object },
            _mockScanner.Object
        );

        string bufferHtml = "<html><body><h1>Direct Editor Buffer</h1></body></html>";
        string sampleJson = "{\"title\": \"Test\"}";

        // Act
        var result = await useCase.PreviewHtmlBufferAsync(bufferHtml, sampleJson, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expectedPdf);

        // Verify zero side-effects
        _mockStorage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTemplateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetHtmlSourceAsync_ShouldDownloadFromStorage_WhenHtmlTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var template = new Template(Guid.NewGuid(), "receipt-template", "receipt-template", null) { Id = templateId };
        template.SetCurrentVersion(versionId);

        var version = new TemplateVersion(templateId, 1, "templates/receipt.html", TemplateFormat.Html, null) { Id = versionId };

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);
        _mockStorage.Setup(s => s.DownloadAsync("templates", "templates/receipt.html", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("<h1>Receipt Source</h1>")));

        var useCase = new HtmlStudioUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockStorage.Object,
            new[] { _mockHtmlEngine.Object },
            _mockScanner.Object
        );

        // Act
        var html = await useCase.GetHtmlSourceAsync(templateId);

        // Assert
        html.Should().Be("<h1>Receipt Source</h1>");
    }
}
