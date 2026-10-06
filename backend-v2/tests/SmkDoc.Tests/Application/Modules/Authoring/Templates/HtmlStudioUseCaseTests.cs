using System.Text;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Factories;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates;

public class HtmlStudioUseCaseTests
{
    private readonly Mock<ITemplateRepository>          _mockTemplateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo  = new();
    private readonly Mock<IStorageService>              _mockStorage      = new();
    private readonly Mock<IRenderEngine>                _mockHtmlEngine   = new();
    private readonly Mock<ITemplateScannerService>      _mockScanner      = new();

    public HtmlStudioUseCaseTests()
    {
        _mockHtmlEngine.SetupGet(e => e.EngineType).Returns(RenderEngineType.Html);
    }

    private HtmlStudioUseCase CreateSut() => new(
        _mockTemplateRepo.Object,
        _mockVersionRepo.Object,
        _mockStorage.Object,
        [_mockHtmlEngine.Object],
        _mockScanner.Object
    );

    [Fact]
    public async Task PreviewHtmlBufferAsync_ShouldRenderInMemory_WithoutTouchingStorageOrDatabase()
    {
        // Arrange
        byte[] expectedPdf = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Stream");
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPdf);

        string bufferHtml = "<html><body><h1>Direct Editor Buffer</h1></body></html>";
        string sampleJson = "{\"title\": \"Test\"}";

        // Act
        var result = await CreateSut().PreviewHtmlBufferAsync(bufferHtml, sampleJson, CancellationToken.None);

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

        var template = TemplateTestFactory.Create(templateId, Guid.NewGuid(), "receipt-template", "receipt-template");
        template.SetCurrentVersion(versionId, TestConstants.BaselineTime);

        var version = TemplateVersionTestFactory.Create(versionId, templateId, 1, "templates/receipt.html", TemplateFormat.Html, createdBy: null);

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);
        _mockStorage.Setup(s => s.DownloadAsync("templates", "templates/receipt.html", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("<h1>Receipt Source</h1>")));

        // Act
        var html = await CreateSut().GetHtmlSourceAsync(templateId);

        // Assert
        html.Should().Be("<h1>Receipt Source</h1>");
    }
}
