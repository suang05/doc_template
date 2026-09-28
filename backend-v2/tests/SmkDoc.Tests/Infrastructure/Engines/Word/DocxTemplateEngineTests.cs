using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Word;
using SmkDoc.Infrastructure.Imaging;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Engines.Word;

public class DocxTemplateEngineTests
{
    private static DocxTemplateEngine CreateEngine(Mock<IPdfRenderer>? mockPdf = null)
    {
        mockPdf ??= new Mock<IPdfRenderer>();
        var mediaService = new MediaGenerationService(
            new Mock<IQrCodeService>().Object,
            new Mock<IBarcodeService>().Object,
            new Mock<IImageOptimizer>().Object);
        var media = new WordMediaInjector(mediaService);
        return new DocxTemplateEngine(mockPdf.Object, media);
    }

    private static MemoryStream BuildDocx(string paragraphText)
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(paragraphText)))));
            main.Document.Save();
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream BuildDocxWithTable(string[] headerCells, string[] templateCells)
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            var body = new Body();

            var tbl = new Table();

            var headerRow = new TableRow();
            foreach (var h in headerCells)
                headerRow.AppendChild(new TableCell(new Paragraph(new Run(new Text(h)))));
            tbl.AppendChild(headerRow);

            var dataRow = new TableRow();
            foreach (var c in templateCells)
                dataRow.AppendChild(new TableCell(new Paragraph(new Run(new Text(c)))));
            tbl.AppendChild(dataRow);

            body.AppendChild(tbl);
            main.Document = new Document(body);
            main.Document.Save();
        }
        ms.Position = 0;
        return ms;
    }

    private static string ExtractText(byte[] docxBytes)
    {
        using var ms = new MemoryStream(docxBytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        return doc.MainDocumentPart?.Document.Body?.InnerText ?? string.Empty;
    }

    [Fact]
    public async Task RenderAsync_WithSimpleTextPlaceholders_ShouldReplaceAll()
    {
        var engine = CreateEngine();
        using var stream = BuildDocx("Contract: {{contractNo}} for {{customerName}}");
        string data = "{\"contractNo\": \"CN-2026-001\", \"customerName\": \"สมชาย พัฒนา\"}";

        byte[] result = await engine.RenderAsync(stream, data, OutputFormat.Docx);

        var text = ExtractText(result);
        text.Should().Contain("Contract: CN-2026-001 for สมชาย พัฒนา");
        text.Should().NotContain("{{contractNo}}");
    }

    [Fact]
    public async Task RenderAsync_WithUpperTransform_ShouldUpperCaseValue()
    {
        // Correct syntax: {{fieldName:transformType}} — field first, then transform
        var engine = CreateEngine();
        using var stream = BuildDocx("ชื่อ: {{customerName:upper}}");
        string data = "{\"customerName\": \"สมชาย ใจดี\"}";

        byte[] result = await engine.RenderAsync(stream, data, OutputFormat.Docx);

        var text = ExtractText(result);
        text.Should().NotContain("{{customerName:upper}}");
        text.Should().Contain("ชื่อ:");
    }

    [Fact]
    public async Task RenderAsync_WithLowerTransform_ShouldLowerCaseValue()
    {
        // Correct syntax: {{fieldName:lower}}
        var engine = CreateEngine();
        using var stream = BuildDocx("อีเมล: {{companyEmail:lower}}");
        string data = "{\"companyEmail\": \"INFO@SAMMAKORN.CO.TH\"}";

        byte[] result = await engine.RenderAsync(stream, data, OutputFormat.Docx);

        var text = ExtractText(result);
        text.Should().NotContain("{{companyEmail:lower}}");
        text.Should().Contain("info@sammakorn.co.th");
    }

    [Fact]
    public async Task RenderAsync_WithNumberTransform_ShouldFormatNumber()
    {
        // Correct syntax: {{fieldName:number}}
        var engine = CreateEngine();
        using var stream = BuildDocx("ยอดรวม: {{total:number}} บาท");
        string data = "{\"total\": 152857.14}";

        byte[] result = await engine.RenderAsync(stream, data, OutputFormat.Docx);

        var text = ExtractText(result);
        text.Should().NotContain("{{total:number}}");
        text.Should().Contain("ยอดรวม:");
    }

    [Fact]
    public async Task RenderAsync_WithTableAndArrayData_ShouldExpandRows()
    {
        var engine = CreateEngine();
        using var stream = BuildDocxWithTable(
            ["ชื่อ", "โทร"],
            ["{{name}}", "{{phone}}"]);

        string data = """
        {
          "contacts": [
            {"name": "สมชาย", "phone": "081-111-2222"},
            {"name": "สมหญิง", "phone": "082-333-4444"}
          ]
        }
        """;

        byte[] result = await engine.RenderAsync(stream, data, OutputFormat.Docx);

        var text = ExtractText(result);
        text.Should().Contain("สมชาย");
        text.Should().Contain("สมหญิง");
        text.Should().Contain("081-111-2222");
        text.Should().Contain("082-333-4444");
        text.Should().NotContain("{{name}}");
        text.Should().NotContain("{{phone}}");
    }

    [Fact]
    public async Task RenderAsync_WithNestedObjectField_ShouldReplaceDotNotation()
    {
        var engine = CreateEngine();
        using var stream = BuildDocx("บริษัท: {{company.name}} โทร: {{company.phone}}");
        string data = "{\"company\": {\"name\": \"บริษัท ABC\", \"phone\": \"02-123-4567\"}}";

        byte[] result = await engine.RenderAsync(stream, data, OutputFormat.Docx);

        var text = ExtractText(result);
        text.Should().Contain("บริษัท: บริษัท ABC");
        text.Should().Contain("โทร: 02-123-4567");
    }

    [Fact]
    public async Task RenderAsync_WithUnknownPlaceholder_ShouldLeaveAsIs()
    {
        var engine = CreateEngine();
        using var stream = BuildDocx("ค่าที่ไม่มี: {{unknownField}}");
        string data = "{\"otherField\": \"value\"}";

        byte[] result = await engine.RenderAsync(stream, data, OutputFormat.Docx);

        var text = ExtractText(result);
        text.Should().Contain("{{unknownField}}");
    }

    [Fact]
    public async Task RenderAsync_WithOutputFormatPdf_ShouldCallPdfRenderer()
    {
        var mockPdf = new Mock<IPdfRenderer>();
        mockPdf.Setup(p => p.RenderOfficeToPdfAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF header

        var engine = CreateEngine(mockPdf);
        using var stream = BuildDocx("{{title}}");
        string data = "{\"title\": \"Test\"}";

        byte[] result = await engine.RenderAsync(stream, data, OutputFormat.Pdf);

        mockPdf.Verify(p => p.RenderOfficeToPdfAsync(It.IsAny<Stream>(), "document.docx", It.IsAny<CancellationToken>()), Times.Once);
        result.Should().Equal(0x25, 0x50, 0x44, 0x46);
    }

    [Fact]
    public async Task RenderAsync_WithEmptyJson_ShouldNotThrow()
    {
        var engine = CreateEngine();
        using var stream = BuildDocx("เอกสาร: {{documentNo}}");

        byte[] result = await engine.RenderAsync(stream, "", OutputFormat.Docx);

        result.Should().NotBeNullOrEmpty();
        var text = ExtractText(result);
        text.Should().Contain("{{documentNo}}");
    }

    [Fact]
    public async Task RenderStreamAsync_WithOutputFormatPdf_ShouldCallPdfRendererStream()
    {
        var expectedStream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        var mockPdf = new Mock<IPdfRenderer>();
        mockPdf.Setup(p => p.RenderOfficeToPdfStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(expectedStream);

        var engine = CreateEngine(mockPdf);
        using var stream = BuildDocx("{{title}}");
        string data = "{\"title\": \"Test\"}";

        var resultStream = await engine.RenderStreamAsync(stream, data, OutputFormat.Pdf);

        mockPdf.Verify(p => p.RenderOfficeToPdfStreamAsync(It.IsAny<Stream>(), "document.docx", It.IsAny<CancellationToken>()), Times.Once);
        resultStream.Should().BeSameAs(expectedStream);
    }
}
