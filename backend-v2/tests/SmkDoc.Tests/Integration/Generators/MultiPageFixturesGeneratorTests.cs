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
using SmkDoc.Infrastructure.Engines.Word;
using SmkDoc.Infrastructure.Imaging;
using Xunit;

namespace SmkDoc.Tests.Integration.Generators;

[Trait("Category", "Generator")]
public class MultiPageFixturesGeneratorTests
{
    private readonly string _outDir = Environment.GetEnvironmentVariable("SMK_FIXTURES_OUT_DIR")
        ?? Path.Combine(Path.GetTempPath(), "smkdoc-tests", "multi-page-samples");

    [Fact]
    public async Task Generate_Word_MultiPage_DefectReport()
    {
        Directory.CreateDirectory(_outDir);
        var docxPath = Path.Combine(_outDir, "02-comprehensive-defect-report.docx");
        var jsonPath = Path.Combine(_outDir, "02-comprehensive-defect-report.json");

        // 1. Build .docx template using OpenXML
        using (var fs = new FileStream(docxPath, FileMode.Create, FileAccess.ReadWrite))
        using (var doc = WordprocessingDocument.Create(fs, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new Body();

            // Document header
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new Bold(), new FontSize { Val = "32" }), new Text("บริษัท สัมมากร จำกัด (มหาชน)"))));
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new Bold(), new FontSize { Val = "24" }, new Color { Val = "0284C7" }), 
                    new Text("รายงานการตรวจสอบข้อบกพร่องสิ่งปลูกสร้างก่อนส่งมอบ (Defect Audit Report)"))));
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new FontSize { Val = "18" }, new Color { Val = "64748B" }), 
                    new Text("เอกสารควบคุมการก่อสร้างและควบคุมคุณภาพ — สายงานวิศวกรรม"))));

            body.AppendChild(new Paragraph(new Run(new Text("------------------------------------------------------------------------------------------------------------------------"))));

            // Metadata info
            body.AppendChild(new Paragraph(
                new Run(new RunProperties(new Bold()), new Text("โครงการ: ")),
                new Run(new Text("{{projectName}}")),
                new Run(new RunProperties(new Bold()), new Text("     โซน/เฟส: ")),
                new Run(new Text("{{phaseName}}")),
                new Run(new RunProperties(new Bold()), new Text("     วันที่ตรวจสอบ: ")),
                new Run(new Text("{{auditDate:thai_date}}"))));

            body.AppendChild(new Paragraph(
                new Run(new RunProperties(new Bold()), new Text("หัวหน้าวิศวกรผู้ตรวจ: ")),
                new Run(new Text("{{leadEngineer}}")),
                new Run(new RunProperties(new Bold()), new Text("     ผู้รับเหมาหลัก: ")),
                new Run(new Text("{{mainContractor}}")),
                new Run(new RunProperties(new Bold()), new Text("     เลขที่รายงาน: ")),
                new Run(new Text("{{reportNo}}"))));

            body.AppendChild(new Paragraph(
                new Run(new RunProperties(new Bold()), new Text("สรุปภาพรวม: ")),
                new Run(new Text("ตรวจพบข้อบกพร่องรวม {{totalDefects}} รายการ (แก้ไขแล้วเสร็จ {{resolvedDefects}} รายการ, คงค้าง {{pendingDefects}} รายการ)"))));

            body.AppendChild(new Paragraph(new Run(new RunProperties(new Bold(), new Color { Val = "0369A1" }), new Text("ตารางรายการตรวจสอบข้อบกพร่องสิ่งปลูกสร้างแบบละเอียด (Detailed Defect Checklist):"))));

            // Dynamic Table
            var table = new Table();
            var tblPr = new TableProperties(new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 2 },
                new InsideVerticalBorder { Val = BorderValues.None }));
            table.AppendChild(tblPr);

            // Header row
            var hRow = new TableRow(new TableRowProperties(new TableHeader()));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text("ลำดับ")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text("แปลง/ห้อง")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text("หมวดงาน")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text("บริเวณพื้นที่")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text("รายละเอียดข้อบกพร่อง")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text("กำหนดแก้เสร็จ")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text("สถานะ")))));
            table.AppendChild(hRow);

            // Dynamic Template Row
            var dRow = new TableRow();
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{no}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{unitNo}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{trade}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{location}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{description}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{targetDate}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{status}}")))));
            table.AppendChild(dRow);

            body.AppendChild(table);

            // Signatures block
            body.AppendChild(new Paragraph(new Run(new Text("------------------------------------------------------------------------------------------------------------------------"))));
            body.AppendChild(new Paragraph(new Run(new RunProperties(new Bold()), new Text("การลงนามรับรองการตรวจสอบความเรียบร้อย:"))));

            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                new Run(new Text("ลงชื่อ .......................................................................... วิศวกรโครงการ (QA Engineer)"))));
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                new Run(new Text("({{leadEngineer}})"))));
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                new Run(new Text("ลงชื่อ .......................................................................... ตัวแทนผู้รับเหมาก่อสร้าง"))));
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                new Run(new Text("({{contractorRep}})"))));

            // Barcode
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new Text("{{barcode:reportNo}}"))));

            mainPart.Document = new Document(body);
            mainPart.Document.Save();
        }

        // 2. Generate 120 Defect Items JSON (Spans ~6-8 pages in Word)
        var trades = new[] { "งานสถาปัตย์", "งานโครงสร้าง", "งานระบบไฟฟ้า", "งานประปาและสุขาภิบาล", "งานสีและพื้นผิว", "งานภูมิทัศน์และรั้ว" };
        var locations = new[] { "ห้องนอนใหญ่ (Master)", "ห้องนั่งเล่นชั้น 1", "ห้องน้ำชั้นบน", "โถงบันได", "ห้องครัวไทย", "ลานจอดรถ", "เฉลียงหน้าบ้าน", "หลังคาและฝ้าภายนอก" };
        var statuses = new[] { "แก้ไขเรียบร้อย", "กำลังดำเนินการ", "รอวัสดุ", "แก้ไขเรียบร้อย", "แก้ไขเรียบร้อย" };

        var defectList = new List<object>();
        for (int i = 1; i <= 120; i++)
        {
            var unitChar = (char)('A' + (i % 8));
            var unitNum = (i % 25) + 1;
            defectList.Add(new
            {
                no = i.ToString(),
                unitNo = $"{unitChar}-{unitNum:D2}",
                trade = trades[i % trades.Length],
                location = locations[i % locations.Length],
                description = $"ตรวจสอบพบข้อบกพร่อง รายการที่ {i}: ปรับระดับรอยต่อและเก็บความเรียบร้อยตามมาตรฐานโครงการ",
                targetDate = $"25/{((i % 12) + 1):D2}/2569",
                status = statuses[i % statuses.Length]
            });
        }

        var payloadObj = new
        {
            projectName = "สัมมากร ชัยพฤกษ์ - วงแหวน (Single House Premium)",
            phaseName = "Phase 2 (โซนสวนสาธารณะ)",
            auditDate = "2026-09-18",
            leadEngineer = "นาย ธนาวุฒิ เกียรติไพศาล (วศ.บ. โยธา)",
            mainContractor = "บริษัท พรีเมียร์ เอ็นจิเนียริ่ง จำกัด",
            contractorRep = "นาย สมศักดิ์ กิจเจริญ",
            reportNo = "QC-AUDIT-2026-098",
            totalDefects = "120",
            resolvedDefects = "96",
            pendingDefects = "24",
            defects = defectList
        };

        var jsonString = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, jsonString);

        // 3. Render and verify using DocxTemplateEngine
        var mediaService = new MediaGenerationService(new Mock<IQrCodeService>().Object, new Mock<IBarcodeService>().Object, new Mock<IImageOptimizer>().Object);
        var mediaInjector = new WordMediaInjector(mediaService);
        var engine = new DocxTemplateEngine(new Mock<IPdfRenderer>().Object, mediaInjector);

        using var templateStream = File.OpenRead(docxPath);
        var renderedBytes = await engine.RenderAsync(templateStream, jsonString, OutputFormat.Docx);

        var outputDocxPath = Path.Combine(_outDir, "output-02-comprehensive-defect-report.docx");
        await File.WriteAllBytesAsync(outputDocxPath, renderedBytes);

        renderedBytes.Should().NotBeNullOrEmpty();
        renderedBytes.Length.Should().BeGreaterThan(2000);
        File.Exists(outputDocxPath).Should().BeTrue();
    }

    [Fact]
    public async Task Generate_Excel_MultiPage_ProjectCashflowModel()
    {
        Directory.CreateDirectory(_outDir);
        var xlsxPath = Path.Combine(_outDir, "03-project-cashflow-model.xlsx");
        var jsonPath = Path.Combine(_outDir, "03-project-cashflow-model.json");

        // 1. Build .xlsx template with ClosedXML
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("CashflowSchedule");
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape; // Landscape for cashflow columns
            ws.PageSetup.SetRowsToRepeatAtTop(1, 5); // Repeat header rows on every page!

            // Title block
            ws.Cell("A1").Value = "บริษัท สัมมากร จำกัด (มหาชน)";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;
            ws.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#0284C7");

            ws.Cell("A2").Value = "แผนการจำลองกระแสเงินสดและการผ่อนชำระโครงการ (MULTI-PAGE PROJECT CASHFLOW MODEL)";
            ws.Cell("A2").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.FontSize = 11;

            ws.Cell("A3").Value = "โครงการ: {{projectName}} | รหัสรายงาน: {{reportId}} | วันที่ออกรายงาน: {{generatedDate}}";
            ws.Cell("A3").Style.Font.FontColor = XLColor.FromHtml("#64748B");

            // Column Header
            ws.Cell("A5").Value = "งวดที่";
            ws.Cell("B5").Value = "กำหนดชำระ";
            ws.Cell("C5").Value = "แปลง/ห้อง";
            ws.Cell("D5").Value = "ชื่อลูกค้า";
            ws.Cell("E5").Value = "รายการเงินงวด";
            ws.Cell("F5").Value = "เงินต้น (บาท)";
            ws.Cell("G5").Value = "ดอกเบี้ย (บาท)";
            ws.Cell("H5").Value = "ยอดรวมผ่อน (บาท)";
            ws.Cell("I5").Value = "สถานะ";

            ws.Range("A5:I5").Style.Font.Bold = true;
            ws.Range("A5:I5").Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
            ws.Range("A5:I5").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // Dynamic Template Row
            ws.Cell("A6").Value = "{{items.no}}";
            ws.Cell("B6").Value = "{{items.dueDate}}";
            ws.Cell("C6").Value = "{{items.unitNo}}";
            ws.Cell("D6").Value = "{{items.customer}}";
            ws.Cell("E6").Value = "{{items.title}}";
            ws.Cell("F6").Value = "{{items.principal}}";
            ws.Cell("G6").Value = "{{items.interest}}";
            ws.Cell("H6").Value = "{{items.totalAmount}}";
            ws.Cell("I6").Value = "{{items.status}}";

            // Total Summary Row with Formulas
            ws.Cell("E7").Value = "ยอดรวมทั้งสิ้น (TOTAL):";
            ws.Cell("E7").Style.Font.Bold = true;
            ws.Cell("F7").FormulaA1 = "=SUM(F6:F6)";
            ws.Cell("F7").Style.Font.Bold = true;
            ws.Cell("G7").FormulaA1 = "=SUM(G6:G6)";
            ws.Cell("G7").Style.Font.Bold = true;
            ws.Cell("H7").FormulaA1 = "=SUM(H6:H6)";
            ws.Cell("H7").Style.Font.Bold = true;

            ws.Column(1).Width = 8;
            ws.Column(2).Width = 14;
            ws.Column(3).Width = 12;
            ws.Column(4).Width = 24;
            ws.Column(5).Width = 22;
            ws.Column(6).Width = 18;
            ws.Column(7).Width = 18;
            ws.Column(8).Width = 20;
            ws.Column(9).Width = 14;

            wb.SaveAs(xlsxPath);
        }

        // 2. Generate 150 Cashflow Items JSON (Spans ~5-6 pages in Excel landscape)
        var customerNames = new[] { "คุณพงศกร วิชัยดิษฐ์", "คุณกัญญารัตน์ อัครเดช", "คุณธนกร สมประสงค์", "คุณวรินทร จินดามณี", "คุณสมชาย มหาศาล", "คุณปิยะดา รัตนกูล" };
        var installments = new List<object>();

        for (int i = 1; i <= 150; i++)
        {
            var unitChar = (char)('A' + (i % 6));
            var unitNum = (i % 30) + 1;
            decimal principal = 40000 + ((i * 350) % 15000);
            decimal interest = 8500 + ((i * 120) % 3000);
            decimal total = principal + interest;

            var day = (i % 28) + 1;
            var month = ((i % 12) + 1);
            var year = 2026 + (i / 36);

            installments.Add(new
            {
                no = i.ToString(),
                dueDate = $"{day:D2}/{month:D2}/{year}",
                unitNo = $"{unitChar}-{unitNum:D2}",
                customer = customerNames[i % customerNames.Length],
                title = $"เงินดาวน์งวดที่ {i}",
                principal = principal.ToString("F2"),
                interest = interest.ToString("F2"),
                totalAmount = total.ToString("F2"),
                status = (i % 10 == 0 ? "รอชำระ" : "ชำระแล้ว")
            });
        }

        var payloadObj = new
        {
            projectName = "สัมมากร รามคำแหง - วงแหวน (Sammakorn Ramkhamhaeng)",
            reportId = "CF-MODEL-2026-150",
            generatedDate = "18/09/2026 16:45",
            items = installments
        };

        var jsonString = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, jsonString);

        // 3. Render and verify using ExcelTemplateEngine
        var excelMediaService = new MediaGenerationService(new Mock<IQrCodeService>().Object, new Mock<IBarcodeService>().Object, new Mock<IImageOptimizer>().Object);
        var mediaInjector = new ExcelMediaInjector(excelMediaService);
        var engine = new ExcelTemplateEngine(new Mock<IPdfRenderer>().Object, mediaInjector);

        using var templateStream = File.OpenRead(xlsxPath);
        var renderedBytes = await engine.RenderAsync(templateStream, jsonString, OutputFormat.Xlsx);

        var outputXlsxPath = Path.Combine(_outDir, "output-03-project-cashflow-model.xlsx");
        await File.WriteAllBytesAsync(outputXlsxPath, renderedBytes);

        renderedBytes.Should().NotBeNullOrEmpty();
        renderedBytes.Length.Should().BeGreaterThan(5000);
        File.Exists(outputXlsxPath).Should().BeTrue();
    }
}
