using System.Text;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Html;
using SmkDoc.Infrastructure.Engines.Html.Helpers;
using SmkDoc.Infrastructure.Imaging;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Engines.Html;

public class HtmlTemplateEngineTests
{
    private static HtmlTemplateEngine CreateEngine(IPdfRenderer pdfRenderer, ICompiledTemplateCache? cache = null)
    {
        var mediaService = new MediaGenerationService(
            new Mock<IQrCodeService>().Object,
            new Mock<IBarcodeService>().Object,
            new ImageOptimizerService());
        var registry = new HtmlHelperRegistry(mediaService);
        return cache != null
            ? new HtmlTemplateEngine(pdfRenderer, registry, cache)
            : new HtmlTemplateEngine(pdfRenderer, registry);
    }

    private static async Task<string> RenderAndCaptureHtmlAsync(
        string templateHtml, 
        string json, 
        ICompiledTemplateCache? cache = null)
    {
        var mockPdf = new Mock<IPdfRenderer>();
        string capturedHtml = string.Empty;

        mockPdf.Setup(p => p.RenderHtmlToPdfStreamAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, string?, CancellationToken>((h, _, _, _) => capturedHtml = h)
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 mock")));

        var engine = CreateEngine(mockPdf.Object, cache);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(templateHtml));
        var result = await engine.RenderAsync(stream, json, OutputFormat.Pdf);

        result.Should().NotBeNull();
        return capturedHtml;
    }

    [Fact]
    public async Task RenderAsync_ShouldSubstituteVariables_AndInjectThaiFont()
    {
        string template = "<html><head></head><body><h1>Hello {{customer.name}}, Total: {{total}}</h1></body></html>";
        string inputData = "{\"customer\": {\"name\": \"สมชาย ใจดี\"}, \"total\": \"2,500,000\"}";

        var capturedHtml = await RenderAndCaptureHtmlAsync(template, inputData);

        capturedHtml.Should().Contain("Hello สมชาย ใจดี, Total: 2,500,000");
        capturedHtml.Should().Contain("fonts.googleapis.com/css2?family=Sarabun");
    }

    [Fact]
    public async Task RenderAsync_WithAddOneHelper_IncrementsIndex()
    {
        string template = @"
            {{#each items}}
                <span>Item {{addOne @index}}: {{name}}</span>
            {{/each}}";

        string inputData = @"{
            ""items"": [
                { ""name"": ""บ้านเดี่ยว"" },
                { ""name"": ""ทาวน์โฮม"" }
            ]
        }";

        var capturedHtml = await RenderAndCaptureHtmlAsync(template, inputData);

        capturedHtml.Should().Contain("Item 1: บ้านเดี่ยว");
        capturedHtml.Should().Contain("Item 2: ทาวน์โฮม");
    }

    [Fact]
    public async Task RenderAsync_WithIfEqualsHelper_EvaluatesCondition()
    {
        string template = @"
            {{#ifEquals status 'APPROVED'}}
                <div>STATUS_OK</div>
            {{else}}
                <div>STATUS_WAITING</div>
            {{/ifEquals}}";

        string inputData = @"{ ""status"": ""APPROVED"" }";

        var capturedHtml = await RenderAndCaptureHtmlAsync(template, inputData);

        capturedHtml.Should().Contain("STATUS_OK");
        capturedHtml.Should().NotContain("STATUS_WAITING");
    }

    [Fact]
    public async Task RenderAsync_WithTemplateCache_UsesCachedEvaluation()
    {
        var mockCache = new Mock<ICompiledTemplateCache>();
        mockCache.Setup(c => c.GetOrAdd(It.IsAny<string>(), It.IsAny<Func<Func<object, string>>>()))
            .Returns<string, Func<Func<object, string>>>((_, factory) => factory());

        string template = "<html><body><h1>User: {{name}}</h1></body></html>";
        string inputData = "{\"name\": \"Somchai\"}";

        var capturedHtml = await RenderAndCaptureHtmlAsync(template, inputData, mockCache.Object);

        capturedHtml.Should().Contain("User: Somchai");
        mockCache.Verify(c => c.GetOrAdd(It.Is<string>(s => !string.IsNullOrEmpty(s)), It.IsAny<Func<Func<object, string>>>()), Times.Once);
    }

    [Fact]
    public async Task RenderStreamAsync_ShouldDirectlyReturnStreamFromPdfRenderer()
    {
        var mockPdf = new Mock<IPdfRenderer>();
        var expectedStream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 direct stream"));
        mockPdf.Setup(p => p.RenderHtmlToPdfStreamAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStream);

        var engine = CreateEngine(mockPdf.Object);

        string template = "<html><body><h1>Direct Stream</h1></body></html>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(template));
        var resultStream = await engine.RenderStreamAsync(stream, "{}", OutputFormat.Pdf);

        resultStream.Should().BeSameAs(expectedStream);
    }
}
