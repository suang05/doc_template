using System.Text;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Html;
using SmkDoc.Infrastructure.Engines.Html.Helpers;
using SmkDoc.Infrastructure.Imaging;
using Xunit;

namespace SmkDoc.Tests;

public class HtmlTemplateEngineTests
{
    private static HtmlTemplateEngine CreateEngine(IPdfRenderer pdfRenderer)
    {
        var mediaService = new MediaGenerationService(
            new Mock<IQrCodeService>().Object,
            new Mock<IBarcodeService>().Object,
            new ImageOptimizerService());
        var registry = new HtmlHelperRegistry(mediaService);
        return new HtmlTemplateEngine(pdfRenderer, registry);
    }

    [Fact]
    public async Task RenderAsync_ShouldSubstituteVariables_AndInjectThaiFont()
    {
        var mockPdf = new Mock<IPdfRenderer>();
        string capturedHtml = string.Empty;

        mockPdf.Setup(p => p.RenderHtmlToPdfAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, string?, CancellationToken>((h, _1, _2, _3) => capturedHtml = h)
            .ReturnsAsync(Encoding.UTF8.GetBytes("%PDF-1.4 mock"));

        var engine = CreateEngine(mockPdf.Object);

        string template = "<html><head></head><body><h1>Hello {{customer.name}}, Total: {{total}}</h1></body></html>";
        string inputData = "{\"customer\": {\"name\": \"สมชาย ใจดี\"}, \"total\": \"2,500,000\"}";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(template));
        var result = await engine.RenderAsync(stream, inputData, OutputFormat.Pdf);

        result.Should().NotBeNull();
        capturedHtml.Should().Contain("Hello สมชาย ใจดี, Total: 2,500,000");
        capturedHtml.Should().Contain("fonts.googleapis.com/css2?family=Sarabun");
    }

    [Fact]
    public async Task RenderAsync_ShouldSupportCustomHelpers_AddOne_And_IfEquals()
    {
        var mockPdf = new Mock<IPdfRenderer>();
        string capturedHtml = string.Empty;

        mockPdf.Setup(p => p.RenderHtmlToPdfAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, string?, CancellationToken>((h, _1, _2, _3) => capturedHtml = h)
            .ReturnsAsync(Encoding.UTF8.GetBytes("%PDF-1.4 mock"));

        var engine = CreateEngine(mockPdf.Object);

        string template = @"
            {{#each items}}
                <span>Item {{addOne @index}}: {{name}}</span>
            {{/each}}
            {{#ifEquals status 'APPROVED'}}
                <div>STATUS_OK</div>
            {{else}}
                <div>STATUS_WAITING</div>
            {{/ifEquals}}";

        string inputData = @"{
            ""status"": ""APPROVED"",
            ""items"": [
                { ""name"": ""บ้านเดี่ยว"" },
                { ""name"": ""ทาวน์โฮม"" }
            ]
        }";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(template));
        var result = await engine.RenderAsync(stream, inputData, OutputFormat.Pdf);

        result.Should().NotBeNull();
        capturedHtml.Should().Contain("Item 1: บ้านเดี่ยว");
        capturedHtml.Should().Contain("Item 2: ทาวน์โฮม");
        capturedHtml.Should().Contain("STATUS_OK");
        capturedHtml.Should().NotContain("STATUS_WAITING");
    }
}
