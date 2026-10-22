using ClosedXML.Excel;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Excel;
using SmkDoc.Infrastructure.Imaging;

namespace SmkDoc.Tests.Infrastructure.Engines.Excel;

public sealed class ExcelTemplateEngineTests
{
    private static ExcelTemplateEngine CreateEngine(
        IQrCodeService? qr = null,
        IBarcodeService? bc = null,
        IImageOptimizer? opt = null,
        IPdfRenderer? pdf = null)
    {
        qr ??= Substitute.For<IQrCodeService>();
        bc ??= Substitute.For<IBarcodeService>();
        opt ??= Substitute.For<IImageOptimizer>();
        pdf ??= Substitute.For<IPdfRenderer>();

        // Default optimizer returns the input bytes unchanged
        opt.Optimize(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>())
           .Returns(callInfo => callInfo.Arg<byte[]>());

        var mediaService = new MediaGenerationService(qr, bc, opt);
        var mediaInjector = new ExcelMediaInjector(mediaService);
        return new ExcelTemplateEngine(pdf, mediaInjector);
    }

    private static MemoryStream BuildWorkbook(Action<IXLWorksheet> setup)
    {
        var stream = new MemoryStream();
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        setup(ws);
        wb.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static async Task<XLWorkbook> RenderWorkbookAsync(
        Action<IXLWorksheet> setup, 
        string json, 
        ExcelTemplateEngine? customEngine = null)
    {
        var engine = customEngine ?? CreateEngine();
        using var template = BuildWorkbook(setup);
        var result = await engine.RenderAsync(template, json, OutputFormat.Xlsx);
        result.Should().NotBeNullOrEmpty();
        return new XLWorkbook(new MemoryStream(result));
    }

    // ── Flat text replacement ─────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithFlatPlaceholders_ShouldReplaceAll()
    {
        using var wb = await RenderWorkbookAsync(ws =>
        {
            ws.Cell("A1").Value = "Invoice No: {{invoiceNo}}";
            ws.Cell("B1").Value = "Amount: {{totalAmount}}";
        }, "{\"invoiceNo\": \"INV-9999\", \"totalAmount\": \"1,250.00\"}");

        var ws = wb.Worksheet("Sheet1");
        ws.Cell("A1").GetString().Should().Be("Invoice No: INV-9999");
        ws.Cell("B1").GetString().Should().Be("Amount: 1,250.00");
    }

    // ── Array / table expansion ───────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithArrayData_ShouldExpandRows()
    {
        string json = """
            {
              "items": [
                {"name": "Widget A", "qty": "2"},
                {"name": "Widget B", "qty": "5"},
                {"name": "Widget C", "qty": "1"}
              ]
            }
            """;

        using var wb = await RenderWorkbookAsync(ws =>
        {
            ws.Cell("A1").Value = "Product";
            ws.Cell("B1").Value = "Qty";
            ws.Cell("A2").Value = "{{items.name}}";
            ws.Cell("B2").Value = "{{items.qty}}";
        }, json);

        var ws = wb.Worksheet("Sheet1");

        // Header row unchanged
        ws.Cell("A1").GetString().Should().Be("Product");
        ws.Cell("B1").GetString().Should().Be("Qty");

        // Expanded rows
        ws.Cell("A2").GetString().Should().Be("Widget A");
        ws.Cell("B2").GetString().Should().Be("2");
        ws.Cell("A3").GetString().Should().Be("Widget B");
        ws.Cell("B3").GetString().Should().Be("5");
        ws.Cell("A4").GetString().Should().Be("Widget C");
        ws.Cell("B4").GetString().Should().Be("1");
    }

    [Fact]
    public async Task RenderAsync_WithArrayAndFlatData_ShouldExpandAndReplaceIndependently()
    {
        string json = """
            {
              "invoiceNo": "INV-0042",
              "lines": [
                {"description": "Service A", "amount": "500"},
                {"description": "Service B", "amount": "300"}
              ]
            }
            """;

        using var wb = await RenderWorkbookAsync(ws =>
        {
            ws.Cell("A1").Value = "Invoice: {{invoiceNo}}";
            ws.Cell("A2").Value = "{{lines.description}}";
            ws.Cell("B2").Value = "{{lines.amount}}";
        }, json);

        var ws = wb.Worksheet("Sheet1");

        ws.Cell("A1").GetString().Should().Be("Invoice: INV-0042");
        ws.Cell("A2").GetString().Should().Be("Service A");
        ws.Cell("B2").GetString().Should().Be("500");
        ws.Cell("A3").GetString().Should().Be("Service B");
        ws.Cell("B3").GetString().Should().Be("300");
    }

    [Fact]
    public async Task RenderAsync_WithEmptyArray_ShouldDeleteTemplateRow()
    {
        using var wb = await RenderWorkbookAsync(ws =>
        {
            ws.Cell("A1").Value = "Header";
            ws.Cell("A2").Value = "{{rows.name}}";
            ws.Cell("A3").Value = "Footer";
        }, "{\"rows\": []}");

        var ws = wb.Worksheet("Sheet1");

        ws.Cell("A1").GetString().Should().Be("Header");
        // Template row deleted — Footer shifts up to A2
        ws.Cell("A2").GetString().Should().Be("Footer");
    }

    // ── QR / Barcode injection ────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithQrPlaceholder_ShouldCallQrServiceAndClearCell()
    {
        var qr = Substitute.For<IQrCodeService>();
        var opt = Substitute.For<IImageOptimizer>();

        qr.Generate("SCAN123", Arg.Any<int>())
          .Returns([0x89, 0x50, 0x4E, 0x47]); // PNG header stub
        opt.Optimize(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>())
           .Returns(callInfo => callInfo.Arg<byte[]>());

        var engine = CreateEngine(qr: qr, opt: opt);

        using var wb = await RenderWorkbookAsync(ws =>
        {
            ws.Cell("A1").Value = "{{qr:code}}";
            ws.Cell("B1").Value = "Other: {{label}}";
        }, "{\"code\": \"SCAN123\", \"label\": \"Hello\"}", engine);

        qr.Received(1).Generate("SCAN123", Arg.Any<int>());
        opt.Received(1).Optimize(Arg.Any<byte[]>(), 150, 150);

        var ws = wb.Worksheet("Sheet1");
        ws.Cell("A1").GetString().Should().BeEmpty(); // cleared after image embed
        ws.Cell("B1").GetString().Should().Be("Other: Hello");
    }

    [Fact]
    public async Task RenderAsync_WithBarcodeAndMissingKey_ShouldNotCallBarcodeService()
    {
        var bc = Substitute.For<IBarcodeService>();
        var engine = CreateEngine(bc: bc);

        using var template = BuildWorkbook(ws =>
        {
            ws.Cell("A1").Value = "{{barcode:serialNo}}";
        });

        // JSON has no "serialNo" key
        await engine.RenderAsync(template, "{\"other\": \"value\"}", OutputFormat.Xlsx);

        bc.DidNotReceive().Generate(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task RenderAsync_PreservesCustomOrientationAndCustomFit()
    {
        using var wb = await RenderWorkbookAsync(ws =>
        {
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PagesWide = 2;
            ws.PageSetup.PagesTall = 1;
            ws.Cell("A1").Value = "Header: {{title}}";
        }, """{"title": "Landscape Report"}""");

        var ws = wb.Worksheet("Sheet1");

        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Landscape);
        ws.PageSetup.PagesWide.Should().Be(2);
        ws.PageSetup.PagesTall.Should().Be(1);
        ws.Cell("A1").GetString().Should().Be("Header: Landscape Report");
    }

    [Fact]
    public async Task RenderStreamAsync_WithOutputFormatPdf_ShouldCallPdfRendererStream()
    {
        var expectedStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        var pdf = Substitute.For<IPdfRenderer>();
        pdf.RenderOfficeToPdfStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(expectedStream);

        var engine = CreateEngine(pdf: pdf);
        using var template = BuildWorkbook(ws =>
        {
            ws.Cell("A1").Value = "Header: {{title}}";
        });

        string json = """{"title": "Direct Stream Report"}""";

        var resultStream = await engine.RenderStreamAsync(template, json, OutputFormat.Pdf);

        await pdf.Received(1).RenderOfficeToPdfStreamAsync(Arg.Any<Stream>(), "spreadsheet.xlsx", Arg.Any<CancellationToken>());
        resultStream.Should().BeSameAs(expectedStream);
    }
}
