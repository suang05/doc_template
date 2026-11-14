using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmkDoc.Infrastructure.Engines.Word;
using SmkDoc.Infrastructure.Imaging;
using SmkDoc.Infrastructure.Parsing;

namespace SmkDoc.Tests.Infrastructure.Engines.Word;

public sealed class DocxTemplateEngineTests
{
    private static DocxTemplateEngine CreateEngine(IPdfRenderer? pdfRenderer = null)
    {
        pdfRenderer ??= Substitute.For<IPdfRenderer>();
        var mediaService = new MediaGenerationService(
            Substitute.For<IQrCodeService>(),
            Substitute.For<IBarcodeService>(),
            Substitute.For<IImageOptimizer>());
        var media = new WordMediaInjector(mediaService);
        return new DocxTemplateEngine(
            pdfRenderer,
            media,
            new WordTextReplacer(),
            new WordTableExpander(),
            new JsonTemplateDataParser());
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

    private static string ExtractText(Stream docxStream)
    {
        docxStream.Position = 0;
        using var doc = WordprocessingDocument.Open(docxStream, false);
        return doc.MainDocumentPart?.Document.Body?.InnerText ?? string.Empty;
    }

    private static async Task<string> RenderToTextAsync(string paragraphText, string json)
    {
        var engine = CreateEngine();
        using var stream = BuildDocx(paragraphText);
        await using var result = await engine.RenderStreamAsync(stream, json, OutputFormat.Docx, CancellationToken.None);
        return ExtractText(result);
    }

    [Theory]
    [InlineData("Contract: {{contractNo}} for {{customerName}}", "{\"contractNo\": \"CN-2026-001\", \"customerName\": \"สมชาย พัฒนา\"}", "Contract: CN-2026-001 for สมชาย พัฒนา")]
    [InlineData("ชื่อ: {{customerName:upper}}", "{\"customerName\": \"somchai\"}", "ชื่อ: SOMCHAI")]
    [InlineData("อีเมล: {{companyEmail:lower}}", "{\"companyEmail\": \"INFO@SAMMAKORN.CO.TH\"}", "อีเมล: info@sammakorn.co.th")]
    [InlineData("ยอดรวม: {{total:number}} บาท", "{\"total\": 152857.14}", "ยอดรวม: 152,857.14 บาท")]
    [InlineData("บริษัท: {{company.name}} โทร: {{company.phone}}", "{\"company\": {\"name\": \"บริษัท ABC\", \"phone\": \"02-123-4567\"}}", "บริษัท: บริษัท ABC โทร: 02-123-4567")]
    [InlineData("ค่าที่ไม่มี: {{unknownField}}", "{\"otherField\": \"value\"}", "ค่าที่ไม่มี: {{unknownField}}")]
    public async Task RenderAsync_WithTextPlaceholdersAndTransforms_SubstitutesExpectedText(string template, string json, string expectedSnippet)
    {
        var text = await RenderToTextAsync(template, json);
        text.Should().Contain(expectedSnippet);
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

        await using var result = await engine.RenderStreamAsync(stream, data, OutputFormat.Docx, CancellationToken.None);

        var text = ExtractText(result);
        text.Should().Contain("สมชาย");
        text.Should().Contain("สมหญิง");
        text.Should().Contain("081-111-2222");
        text.Should().Contain("082-333-4444");
        text.Should().NotContain("{{name}}");
        text.Should().NotContain("{{phone}}");
    }

    [Fact]
    public async Task RenderAsync_WithEmptyJson_ShouldNotThrow()
    {
        var engine = CreateEngine();
        using var stream = BuildDocx("เอกสาร: {{documentNo}}");

        await using var result = await engine.RenderStreamAsync(stream, "", OutputFormat.Docx, CancellationToken.None);

        result.Length.Should().BeGreaterThan(0);
        var text = ExtractText(result);
        text.Should().Contain("{{documentNo}}");
    }

    [Fact]
    public async Task RenderStreamAsync_WithOutputFormatPdf_ShouldCallPdfRendererStream()
    {
        var expectedStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        var pdfRenderer = Substitute.For<IPdfRenderer>();
        pdfRenderer.RenderOfficeToPdfStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(expectedStream);

        var engine = CreateEngine(pdfRenderer);
        using var stream = BuildDocx("{{title}}");
        string data = "{\"title\": \"Test\"}";

        var resultStream = await engine.RenderStreamAsync(stream, data, OutputFormat.Pdf, CancellationToken.None);

        await pdfRenderer.Received(1).RenderOfficeToPdfStreamAsync(Arg.Any<Stream>(), "document.docx", Arg.Any<CancellationToken>());
        resultStream.Should().BeSameAs(expectedStream);
    }
}
