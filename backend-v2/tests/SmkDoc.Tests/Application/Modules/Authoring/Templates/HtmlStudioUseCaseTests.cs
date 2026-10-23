using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates;

public sealed class HtmlStudioUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly IRenderEngine _htmlEngine = Substitute.For<IRenderEngine>();
    private readonly ITemplateScannerService _scanner = Substitute.For<ITemplateScannerService>();

    public HtmlStudioUseCaseTests()
    {
        _htmlEngine.EngineType.Returns(RenderEngineType.Html);
    }

    private HtmlStudioUseCase CreateSut() => new(
        _templateRepo,
        _storageService,
        [_htmlEngine],
        _scanner);

    [Fact]
    public async Task PreviewHtmlBufferAsync_ShouldRenderInMemory_WithoutTouchingStorageOrDatabase()
    {
        // Arrange
        byte[] expectedPdf = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Stream");
        _htmlEngine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(expectedPdf));

        string bufferHtml = "<html><body><h1>Direct Editor Buffer</h1></body></html>";
        string sampleJson = "{\"title\": \"Test\"}";

        // Act
        var result = await CreateSut().PreviewHtmlBufferAsync(bufferHtml, sampleJson, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expectedPdf);

        // Verify zero side-effects
        await _storageService.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewHtmlStreamAsync_ShouldRenderStream_WithoutTouchingStorageOrDatabase()
    {
        // Arrange
        byte[] expectedPdf = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Stream");
        _htmlEngine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(expectedPdf));

        string bufferHtml = "<html><body><h1>Direct Editor Buffer</h1></body></html>";
        string sampleJson = "{\"title\": \"Test\"}";

        // Act
        await using var stream = await CreateSut().PreviewHtmlStreamAsync(bufferHtml, sampleJson, CancellationToken.None);

        // Assert
        stream.Should().NotBeNull();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(expectedPdf);

        // Verify zero side-effects
        await _storageService.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHtmlSourceAsync_ShouldDownloadFromStorage_WhenHtmlTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithSlug("receipt-template")
            .WithName("receipt-template");
        template.SetCurrentVersion(versionId, TestConstants.BaselineTime);

        TemplateVersion version = TemplateVersionBuilder.AVersion()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(1)
            .WithStorageKey("templates/receipt.html")
            .WithFormat(TemplateFormat.Html)
            .WithCreatedBy(null);

        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(version);
        _storageService.DownloadAsync("templates", "templates/receipt.html", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("<h1>Receipt Source</h1>")));

        // Act
        var html = await CreateSut().GetHtmlSourceAsync(templateId);

        // Assert
        html.Should().Be("<h1>Receipt Source</h1>");
    }
}
