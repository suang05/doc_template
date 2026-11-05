using FluentAssertions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Infrastructure.Engines.Html.Pipeline;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Engines.Html.Pipeline;

public sealed class HtmlLayoutProcessorTests
{
    private readonly HtmlLayoutProcessor _sut = new();

    [Fact]
    public void Process_WithEmptyOrWhitespaceHtml_ShouldReturnEmptyWithSettingsHeaders()
    {
        var settings = new TemplatePageSettings(
            HeaderHtml: "<div>Default Header</div>",
            FooterHtml: "<div>Default Footer</div>");

        var result = _sut.Process("   ", settings);

        result.BodyHtml.Should().BeEmpty();
        result.HeaderHtml.Should().Be("<div>Default Header</div>");
        result.FooterHtml.Should().Be("<div>Default Footer</div>");
    }

    [Fact]
    public void Process_WithoutAtPageRule_ShouldInjectCssAtPageRule_MatchingSettings()
    {
        var settings = new TemplatePageSettings(
            PaperSize: "A3",
            Orientation: "landscape",
            MarginTopMm: 20.0,
            MarginBottomMm: 25.0,
            MarginLeftMm: 12.0,
            MarginRightMm: 14.0);

        string html = "<html><head><title>Test</title></head><body><h1>Content</h1></body></html>";

        var result = _sut.Process(html, settings);

        result.BodyHtml.Should().Contain("@page { size: A3 landscape; margin: 20mm 14mm 25mm 12mm; }");
        result.BodyHtml.Should().Contain("<style>@page { size: A3 landscape; margin: 20mm 14mm 25mm 12mm; }</style>");
    }

    [Fact]
    public void Process_WithExistingAtPageRule_ShouldNotDuplicateAtPageRule()
    {
        string html = "<html><head><style>@page { size: Letter; }</style></head><body>Hello</body></html>";

        var result = _sut.Process(html, TemplatePageSettings.Default);

        result.BodyHtml.Should().Contain("@page { size: Letter; }");
        result.BodyHtml.Should().NotContain("@page { size: A4");
    }

    [Fact]
    public void Process_WithoutHeadTag_ShouldPrependFontAndPageCss()
    {
        string html = "<div>Fragment Content</div>";

        var result = _sut.Process(html, TemplatePageSettings.Default);

        result.BodyHtml.Should().StartWith("<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css2?family=Sarabun");
        result.BodyHtml.Should().Contain("@page { size: A4 portrait; margin: 15mm 10mm 15mm 10mm; }");
        result.BodyHtml.Should().Contain("<div>Fragment Content</div>");
    }

    [Fact]
    public void Process_WithHeaderAndFooterTemplates_ShouldExtractThemAndStripFromHtml()
    {
        string html = """
            <html>
            <head></head>
            <body>
              <template id="header"><div class="head">My Header</div></template>
              <h1>Main Body</h1>
              <template id="footer"><div class="foot">Page 1</div></template>
            </body>
            </html>
            """;

        var result = _sut.Process(html);

        result.HeaderHtml.Should().Be("<div class=\"head\">My Header</div>");
        result.FooterHtml.Should().Be("<div class=\"foot\">Page 1</div>");
        result.BodyHtml.Should().NotContain("<template id=\"header\">");
        result.BodyHtml.Should().NotContain("<template id=\"footer\">");
        result.BodyHtml.Should().Contain("<h1>Main Body</h1>");
    }

    [Fact]
    public void Process_WithoutHeaderFooterTemplates_ShouldFallbackToSettingsHeaderAndFooter()
    {
        var settings = new TemplatePageSettings(
            HeaderHtml: "<p>Settings Header</p>",
            FooterHtml: "<p>Settings Footer</p>");

        string html = "<html><head></head><body><h1>Report</h1></body></html>";

        var result = _sut.Process(html, settings);

        result.HeaderHtml.Should().Be("<p>Settings Header</p>");
        result.FooterHtml.Should().Be("<p>Settings Footer</p>");
        result.BodyHtml.Should().Contain("<h1>Report</h1>");
    }

    [Fact]
    public void AssembleFullHtmlDocument_WithRawBody_ShouldWrapInFullHtmlDoc_WithPageRuleAndFonts()
    {
        var settings = new TemplatePageSettings(
            PaperSize: "A4",
            Orientation: "portrait",
            HeaderHtml: "<span>Header</span>",
            FooterHtml: "<span>Footer</span>");

        string body = "<h2>Invoice Item</h2>";

        var fullHtml = _sut.AssembleFullHtmlDocument(body, settings);

        fullHtml.Should().Contain("<!DOCTYPE html>");
        fullHtml.Should().Contain("<html lang=\"th\">");
        fullHtml.Should().Contain("@page { size: A4 portrait; margin: 15mm 10mm 15mm 10mm; }");
        fullHtml.Should().Contain("family=Sarabun");
        fullHtml.Should().Contain("<template id=\"header\"><span>Header</span></template>");
        fullHtml.Should().Contain("<h2>Invoice Item</h2>");
        fullHtml.Should().Contain("<template id=\"footer\"><span>Footer</span></template>");
    }

    [Fact]
    public void AssembleFullHtmlDocument_WithAlreadyFullHtmlDocument_ShouldReturnOriginalHtml()
    {
        string fullDocument = "<!DOCTYPE html><html><head><title>Full</title></head><body>Custom</body></html>";

        var result = _sut.AssembleFullHtmlDocument(fullDocument);

        result.Should().Be(fullDocument);
    }

    [Fact]
    public void InjectCustomFontFace_WithValidData_ShouldInjectFontFaceInHead()
    {
        string html = "<html><head><title>Test</title></head><body><h1>Content</h1></body></html>";
        string base64Data = "AAEAAAASAQA...";

        var result = _sut.InjectCustomFontFace(html, "MyCustomFont", base64Data, "truetype");

        result.Should().Contain("@font-face");
        result.Should().Contain("font-family: 'MyCustomFont';");
        result.Should().Contain("src: url('data:font/truetype;charset=utf-8;base64,AAEAAAASAQA...') format('truetype');");
        result.Should().Contain("<head>\n<style>\n@font-face");
    }
}
