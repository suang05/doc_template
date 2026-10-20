using ClosedXML.Excel;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Excel;
using SmkDoc.Infrastructure.Imaging;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Engines.Excel;

public class ExcelTemplateEngineTests
{
    private static ExcelTemplateEngine CreateEngine(
        Mock<IQrCodeService>?  mockQr      = null,
        Mock<IBarcodeService>? mockBc      = null,
        Mock<IImageOptimizer>? mockOpt     = null,
        Mock<IPdfRenderer>?    mockPdf     = null)
    {
        var qr  = mockQr  ?? new Mock<IQrCodeService>();
        var bc  = mockBc  ?? new Mock<IBarcodeService>();
        var opt = mockOpt ?? new Mock<IImageOptimizer>();
        var pdf = mockPdf ?? new Mock<IPdfRenderer>();

        // Default optimizer returns the input bytes unchanged
        opt.Setup(x => x.Optimize(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
           .Returns<byte[], int, int>((b, _, _) => b);

        var mediaService = new MediaGenerationService(qr.Object, bc.Object, opt.Object);
        var mediaInjector = new ExcelMediaInjector(mediaService);
        return new ExcelTemplateEngine(pdf.Object, mediaInjector);
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
        var mockQr  = new Mock<IQrCodeService>();
        var mockOpt = new Mock<IImageOptimizer>();

        mockQr.Setup(x => x.Generate("SCAN123", It.IsAny<int>()))
              .Returns([0x89, 0x50, 0x4E, 0x47]); // PNG header stub
        mockOpt.Setup(x => x.Optimize(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
               .Returns<byte[], int, int>((b, _, _) => b);

        var engine = CreateEngine(mockQr: mockQr, mockOpt: mockOpt);

        using var wb = await RenderWorkbookAsync(ws =>
        {
            ws.Cell("A1").Value = "{{qr:code}}";
            ws.Cell("B1").Value = "Other: {{label}}";
        }, "{\"code\": \"SCAN123\", \"label\": \"Hello\"}", engine);

        mockQr.Verify(x => x.Generate("SCAN123", It.IsAny<int>()), Times.Once);
        mockOpt.Verify(x => x.Optimize(It.IsAny<byte[]>(), 150, 150), Times.Once);

        var ws = wb.Worksheet("Sheet1");
        ws.Cell("A1").GetString().Should().BeEmpty(); // cleared after image embed
        ws.Cell("B1").GetString().Should().Be("Other: Hello");
    }

    [Fact]
    public async Task RenderAsync_WithBarcodeAndMissingKey_ShouldNotCallBarcodeService()
    {
        var mockBc = new Mock<IBarcodeService>();
        var engine = CreateEngine(mockBc: mockBc);

        using var template = BuildWorkbook(ws =>
        {
            ws.Cell("A1").Value = "{{barcode:serialNo}}";
        });

        // JSON has no "serialNo" key
        await engine.RenderAsync(template, "{\"other\": \"value\"}", OutputFormat.Xlsx);

        mockBc.Verify(x => x.Generate(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
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
        var mockPdf = new Mock<IPdfRenderer>();
        mockPdf.Setup(p => p.RenderOfficeToPdfStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(expectedStream);

        var engine = CreateEngine(mockPdf: mockPdf);
        using var template = BuildWorkbook(ws =>
        {
            ws.Cell("A1").Value = "Header: {{title}}";
        });

        string json = """{"title": "Direct Stream Report"}""";

        var resultStream = await engine.RenderStreamAsync(template, json, OutputFormat.Pdf);

        mockPdf.Verify(p => p.RenderOfficeToPdfStreamAsync(It.IsAny<Stream>(), "spreadsheet.xlsx", It.IsAny<CancellationToken>()), Times.Once);
        resultStream.Should().BeSameAs(expectedStream);
    }
}
