using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Excel;
using SmkDoc.Infrastructure.Engines.Html;
using SmkDoc.Infrastructure.Engines.Html.Helpers;
using SmkDoc.Infrastructure.Engines.Word;
using SmkDoc.Infrastructure.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace SmkDoc.IntegrationTests.Benchmarks;

[Trait("Category", "Benchmark")]
public class PerformanceBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static List<object> GenerateTransactions(int count)
    {
        var list = new List<object>(count);
        var baseDate = new DateTime(2026, 1, 1);
        for (int i = 1; i <= count; i++)
        {
            list.Add(new
            {
                no = i.ToString(),
                date = baseDate.AddDays(i % 365).ToString("dd/MM/yyyy"),
                refNo = $"TXN-2026-{i:D6}",
                description = $"ชำระค่างวดที่อยู่อาศัย แปลง {i % 100 + 1:D3} โครงการสัมมากร",
                account = "110-2-34567-8",
                debit = (i % 2 == 0 ? (i * 150.50m).ToString("N2") : "0.00"),
                credit = (i % 2 != 0 ? (i * 200.75m).ToString("N2") : "0.00"),
                amount = (i * 1250.00m).ToString("N2")
            });
        }
        return list;
    }

    [Fact]
    public async Task Benchmark_Excel_1000Rows_Expansion_Performance()
    {
        const int rowCount = 1000;
        var items = GenerateTransactions(rowCount);
        var payload = JsonSerializer.Serialize(new
        {
            companyName = "บริษัท สัมมากร จำกัด (มหาชน)",
            reportTitle = "รายงานบัญชีการทำธุรกรรมรับ-จ่ายเงินประจำปี (1,000 รายการ)",
            generatedDate = "18/09/2026 15:30:00",
            items
        });

        // 1. Build Excel Template
        using var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Transactions");
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;

            ws.Cell("A1").Value = "{{companyName}}";
            ws.Cell("A2").Value = "{{reportTitle}}";
            ws.Cell("A3").Value = "วันที่พิมพ์: {{generatedDate}}";

            ws.Cell("A5").Value = "ลำดับ";
            ws.Cell("B5").Value = "วันที่";
            ws.Cell("C5").Value = "เลขที่อ้างอิง";
            ws.Cell("D5").Value = "รายการ";
            ws.Cell("E5").Value = "จำนวนเงิน (บาท)";

            // Template row
            ws.Cell("A6").Value = "{{items.no}}";
            ws.Cell("B6").Value = "{{items.date}}";
            ws.Cell("C6").Value = "{{items.refNo}}";
            ws.Cell("D6").Value = "{{items.description}}";
            ws.Cell("E6").Value = "{{items.amount}}";

            // Total summary
            ws.Cell("D7").Value = "ยอดรวมทั้งสิ้น:";
            ws.Cell("E7").FormulaA1 = "=SUM(E6:E6)";

            wb.SaveAs(ms);
        }

        var templateBytes = ms.ToArray();

        // 2. Measure Execution
        var excelMediaService = new MediaGenerationService(new Mock<IQrCodeService>().Object, new Mock<IBarcodeService>().Object, new Mock<IImageOptimizer>().Object);
        var mediaInjector = new ExcelMediaInjector(excelMediaService);
        var engine = new ExcelTemplateEngine(new Mock<IPdfRenderer>().Object, mediaInjector);

        // Warm up
        using (var warmStream = new MemoryStream(templateBytes))
        {
            await engine.RenderAsync(warmStream, payload, OutputFormat.Xlsx);
        }

        // Benchmark
        var sw = Stopwatch.StartNew();
        byte[] resultBytes;
        using (var testStream = new MemoryStream(templateBytes))
        {
            resultBytes = await engine.RenderAsync(testStream, payload, OutputFormat.Xlsx);
        }
        sw.Stop();

        var elapsedMs = sw.ElapsedMilliseconds;
        var throughput = (double)rowCount / (elapsedMs / 1000.0);

        _output.WriteLine($"==========================================================");
        _output.WriteLine($"[EXCEL BENCHMARK] 1,000 Rows Dynamic Expansion");
        _output.WriteLine($"  - Elapsed Time       : {elapsedMs} ms ({elapsedMs / 1000.0:F2} seconds)");
        _output.WriteLine($"  - Throughput         : {throughput:F0} rows/second (tx/sec)");
        _output.WriteLine($"  - Output File Size   : {resultBytes.Length / 1024.0:F1} KB ({resultBytes.Length:N0} bytes)");
        _output.WriteLine($"==========================================================");

        resultBytes.Should().NotBeNullOrEmpty();
        resultBytes.Length.Should().BeGreaterThan(20000);
        elapsedMs.Should().BeLessThan(5000); // Must be under 5s
    }

    [Fact]
    public async Task Benchmark_Word_1000Rows_Expansion_Performance()
    {
        const int rowCount = 1000;
        var items = GenerateTransactions(rowCount);
        var payload = JsonSerializer.Serialize(new
        {
            companyName = "บริษัท สัมมากร จำกัด (มหาชน)",
            reportTitle = "บันทึกรายการธุรกรรม (1,000 TRANSACTIONS)",
            items
        });

        // 1. Build Word Template
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new Body();

            body.AppendChild(new Paragraph(new Run(new Text("บริษัท สัมมากร จำกัด (มหาชน)"))));
            body.AppendChild(new Paragraph(new Run(new Text("รายงานธุรกรรมประจำปี 1,000 รายการ"))));

            var table = new Table();
            var hRow = new TableRow();
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("ลำดับ")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("วันที่")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("เลขที่อ้างอิง")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("รายการ")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("จำนวนเงิน")))));
            table.AppendChild(hRow);

            var dRow = new TableRow();
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{items.no}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{items.date}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{items.refNo}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{items.description}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{items.amount}}")))));
            table.AppendChild(dRow);

            body.AppendChild(table);
            mainPart.Document = new Document(body);
            mainPart.Document.Save();
        }

        var templateBytes = ms.ToArray();

        // 2. Measure Execution
        var wordMediaService = new MediaGenerationService(new Mock<IQrCodeService>().Object, new Mock<IBarcodeService>().Object, new Mock<IImageOptimizer>().Object);
        var mediaInjector = new WordMediaInjector(wordMediaService);
        var engine = new DocxTemplateEngine(new Mock<IPdfRenderer>().Object, mediaInjector);

        // Warm up
        using (var warmStream = new MemoryStream(templateBytes))
        {
            await engine.RenderAsync(warmStream, payload, OutputFormat.Docx);
        }

        // Benchmark
        var sw = Stopwatch.StartNew();
        byte[] resultBytes;
        using (var testStream = new MemoryStream(templateBytes))
        {
            resultBytes = await engine.RenderAsync(testStream, payload, OutputFormat.Docx);
        }
        sw.Stop();

        var elapsedMs = sw.ElapsedMilliseconds;
        var throughput = (double)rowCount / (elapsedMs / 1000.0);

        _output.WriteLine($"==========================================================");
        _output.WriteLine($"[WORD BENCHMARK] 1,000 Rows Dynamic Expansion");
        _output.WriteLine($"  - Elapsed Time       : {elapsedMs} ms ({elapsedMs / 1000.0:F2} seconds)");
        _output.WriteLine($"  - Throughput         : {throughput:F0} rows/second (tx/sec)");
        _output.WriteLine($"  - Output File Size   : {resultBytes.Length / 1024.0:F1} KB ({resultBytes.Length:N0} bytes)");
        _output.WriteLine($"==========================================================");

        resultBytes.Should().NotBeNullOrEmpty();
        resultBytes.Length.Should().BeGreaterThan(2000);
        elapsedMs.Should().BeLessThan(5000); // Must be under 5s
    }

    [Fact]
    public async Task Benchmark_Html_1000Rows_Handlebars_Merge_Performance()
    {
        const int rowCount = 1000;
        var items = GenerateTransactions(rowCount);
        var payload = JsonSerializer.Serialize(new
        {
            companyName = "บริษัท สัมมากร จำกัด (มหาชน)",
            reportTitle = "รายงานสรุปธุรกรรม 1,000 แถว",
            items
        });

        var htmlTemplate = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        @page { size: A4; margin: 20mm 15mm; }
        body { font-family: 'Sarabun', sans-serif; font-size: 9pt; }
        table { width: 100%; border-collapse: collapse; }
        th, td { border: 1px solid #cbd5e1; padding: 4px 6px; }
        th { background: #f1f5f9; text-align: left; }
    </style>
</head>
<body>
    <template id='header'>
        <div style='font-size: 8pt; color: #64748b;'>{{companyName}} - {{reportTitle}}</div>
    </template>
    <template id='footer'>
        <div style='font-size: 8pt; text-align: right;'>หน้าที่ <span class='pageNumber'></span> / <span class='totalPages'></span></div>
    </template>
    <h2>{{reportTitle}}</h2>
    <table>
        <thead>
            <tr>
                <th>#</th>
                <th>วันที่</th>
                <th>เลขอ้างอิง</th>
                <th>รายละเอียดธุรกรรม</th>
                <th>จำนวนเงิน</th>
            </tr>
        </thead>
        <tbody>
            {{#each items}}
            <tr>
                <td>{{no}}</td>
                <td>{{date}}</td>
                <td>{{refNo}}</td>
                <td>{{description}}</td>
                <td style='text-align: right;'>{{amount}}</td>
            </tr>
            {{/each}}
        </tbody>
    </table>
</body>
</html>";

        var templateBytes = Encoding.UTF8.GetBytes(htmlTemplate);
        var mockPdf = new Mock<IPdfRenderer>();
        mockPdf.Setup(p => p.RenderHtmlToPdfAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string html, string? h, string? f, CancellationToken ct) => Encoding.UTF8.GetBytes(html));

        var htmlMediaService = new MediaGenerationService(new Mock<IQrCodeService>().Object, new Mock<IBarcodeService>().Object);
        var engine = new HtmlTemplateEngine(mockPdf.Object, new HtmlHelperRegistry(htmlMediaService));

        // Warm up
        using (var warmStream = new MemoryStream(templateBytes))
        {
            await engine.RenderAsync(warmStream, payload, OutputFormat.Pdf);
        }

        // Benchmark
        var sw = Stopwatch.StartNew();
        byte[] resultBytes;
        using (var testStream = new MemoryStream(templateBytes))
        {
            resultBytes = await engine.RenderAsync(testStream, payload, OutputFormat.Pdf);
        }
        sw.Stop();

        var elapsedMs = sw.ElapsedMilliseconds;
        var throughput = (double)rowCount / (elapsedMs / 1000.0);

        _output.WriteLine($"==========================================================");
        _output.WriteLine($"[HTML BENCHMARK] 1,000 Rows Handlebars Template Merge");
        _output.WriteLine($"  - Elapsed Time       : {elapsedMs} ms ({elapsedMs / 1000.0:F2} seconds)");
        _output.WriteLine($"  - Throughput         : {throughput:F0} rows/second (tx/sec)");
        _output.WriteLine($"  - Merged Output Size : {resultBytes.Length / 1024.0:F1} KB ({resultBytes.Length:N0} bytes)");
        _output.WriteLine($"==========================================================");

        resultBytes.Should().NotBeNullOrEmpty();
        resultBytes.Length.Should().BeGreaterThan(50000);
        elapsedMs.Should().BeLessThan(1000); // Handlebars merge should be very fast (< 1s)
    }

    [Fact]
    public async Task Benchmark_Engine_Concurrency_Performance()
    {
        const int concurrentRequests = 50;
        var htmlTemplate = "<html><body><h1>Hello World</h1></body></html>";
        var templateBytes = Encoding.UTF8.GetBytes(htmlTemplate);
        
        var mockPdf = new Mock<IPdfRenderer>();
        mockPdf.Setup(p => p.RenderHtmlToPdfAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string html, string? h, string? f, CancellationToken ct) => Encoding.UTF8.GetBytes(html)); // mock returns bytes

        var htmlMediaService = new MediaGenerationService(new Mock<IQrCodeService>().Object, new Mock<IBarcodeService>().Object);
        var engine = new HtmlTemplateEngine(mockPdf.Object, new HtmlHelperRegistry(htmlMediaService));
        var payload = "{}";

        var sw = Stopwatch.StartNew();
        var tasks = new List<Task<byte[]>>();

        for (int i = 0; i < concurrentRequests; i++)
        {
            var stream = new MemoryStream(templateBytes);
            tasks.Add(engine.RenderAsync(stream, payload, OutputFormat.Pdf));
        }

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        _output.WriteLine($"==========================================================");
        _output.WriteLine($"[CONCURRENCY BENCHMARK] {concurrentRequests} Concurrent HTML->PDF Render Requests");
        _output.WriteLine($"  - Elapsed Time       : {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"==========================================================");

        results.Length.Should().Be(concurrentRequests);
        foreach (var result in results)
        {
            result.Should().NotBeNullOrEmpty();
        }
    }
}
