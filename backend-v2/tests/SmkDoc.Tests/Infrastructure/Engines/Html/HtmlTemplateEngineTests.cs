using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Html;
using SmkDoc.Infrastructure.Engines.Html.Helpers;
using SmkDoc.Infrastructure.Imaging;

namespace SmkDoc.Tests.Infrastructure.Engines.Html;

public sealed class HtmlTemplateEngineTests
{
    private static HtmlTemplateEngine CreateEngine(IPdfRenderer pdfRenderer, ICompiledTemplateCache? cache = null)
    {
        var mediaService = new MediaGenerationService(
            Substitute.For<IQrCodeService>(),
            Substitute.For<IBarcodeService>(),
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
        var pdfRenderer = Substitute.For<IPdfRenderer>();
        string capturedHtml = string.Empty;

        pdfRenderer.RenderHtmlToPdfStreamAsync(
            Arg.Do<string>(h => capturedHtml = h),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 mock")));

        var engine = CreateEngine(pdfRenderer, cache);
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
        var cache = Substitute.For<ICompiledTemplateCache>();
        cache.GetOrAdd(Arg.Any<string>(), Arg.Any<Func<Func<object, string>>>())
            .Returns(callInfo => callInfo.Arg<Func<Func<object, string>>>()());

        string template = "<html><body><h1>User: {{name}}</h1></body></html>";
        string inputData = "{\"name\": \"Somchai\"}";

        var capturedHtml = await RenderAndCaptureHtmlAsync(template, inputData, cache);

        capturedHtml.Should().Contain("User: Somchai");
        cache.Received(1).GetOrAdd(Arg.Is<string>(s => !string.IsNullOrEmpty(s)), Arg.Any<Func<Func<object, string>>>());
    }

    [Fact]
    public async Task RenderStreamAsync_ShouldDirectlyReturnStreamFromPdfRenderer()
    {
        var pdfRenderer = Substitute.For<IPdfRenderer>();
        var expectedStream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 direct stream"));
        pdfRenderer.RenderHtmlToPdfStreamAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        var engine = CreateEngine(pdfRenderer);

        string template = "<html><body><h1>Direct Stream</h1></body></html>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(template));
        var resultStream = await engine.RenderStreamAsync(stream, "{}", OutputFormat.Pdf);

        resultStream.Should().BeSameAs(expectedStream);
    }
}
