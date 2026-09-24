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

namespace SmkDoc.Tests;

public class RealEstateDocumentsGeneratorTests
{
    private readonly string _outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../docs/tests/real-estate"));

    [Fact]
    public async Task GenerateAndVerify_WordTemplate_SalesHandoverNotice()
    {
        Directory.CreateDirectory(_outputDir);
        var docxPath = Path.Combine(_outputDir, "06-sales-handover-letter.docx");
        var jsonPath = Path.Combine(_outputDir, "06-sales-handover-letter.json");

        // 1. Build .docx template using OpenXML
        using (var fs = new FileStream(docxPath, FileMode.Create, FileAccess.ReadWrite))
        using (var doc = WordprocessingDocument.Create(fs, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new Body();

            // Heading
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new Bold(), new FontSize { Val = "32" }), new Text("บริษัท สัมมากร จำกัด (มหาชน)"))));
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new FontSize { Val = "20" }, new Color { Val = "64748B" }), new Text("195 อาคารเอ็มไพร์ ทาวเวอร์ ชั้น 18 สาทรใต้ ยานนาวา สาทร กรุงเทพฯ 10120"))));

            // Horizontal rule
            body.AppendChild(new Paragraph(new Run(new Text("------------------------------------------------------------------------------------------------------------------------"))));

            // Doc Ref & Date
            body.AppendChild(new Paragraph(
                new Run(new RunProperties(new Bold()), new Text("เลขที่เอกสาร: ")),
                new Run(new Text("{{noticeNo}}")),
                new Run(new Text("                                                            ")),
                new Run(new RunProperties(new Bold()), new Text("วันที่: ")),
                new Run(new Text("{{issueDate:thai_date}}"))));

            // Recipient
            body.AppendChild(new Paragraph(
                new Run(new RunProperties(new Bold()), new Text("เรื่อง: ")),
                new Run(new Text("ขอเชิญตรวจสอบความเรียบร้อยสิ่งปลูกสร้างและกำหนดวันจดทะเบียนโอนกรรมสิทธิ์"))));
            body.AppendChild(new Paragraph(
                new Run(new RunProperties(new Bold()), new Text("เรียน: ")),
                new Run(new Text("{{customerName}}"))));
            body.AppendChild(new Paragraph(
                new Run(new RunProperties(new Bold()), new Text("ที่อยู่: ")),
                new Run(new Text("{{customerAddress}}"))));

            // Content
            body.AppendChild(new Paragraph(new Run(new Text("     ตามที่ท่านได้ตกลงทำสัญญาจะซื้อจะขายที่ดินพร้อมสิ่งปลูกสร้างในโครงการ {{projectName}} แปลงเลขที่ {{unitNo}} แบบบ้าน {{houseModel}} โฉนดที่ดินเลขที่ {{titleDeedNo}} กับบริษัทฯ นั้น"))));
            body.AppendChild(new Paragraph(new Run(new Text("     บัดนี้ การก่อสร้างบ้านเดี่ยวดังกล่าวได้ดำเนินการแล้วเสร็จสมบูรณ์ตามมาตรฐานของโครงการเรียบร้อยแล้ว บริษัทฯ จึงใคร่ขอเรียนเชิญท่านเข้าตรวจสอบความเรียบร้อยและกำหนดการจดทะเบียนโอนกรรมสิทธิ์ ณ สำนักงานที่ดิน{{landOfficeName}} ในวันที่ {{transferDate:thai_date}}"))));

            body.AppendChild(new Paragraph(new Run(new RunProperties(new Bold()), new Text("สรุปค่าใช้จ่ายและยอดเงินที่ต้องชำระ ณ วันโอนกรรมสิทธิ์:"))));

            // Table of expenses
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
            var hRow = new TableRow();
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text("ลำดับ")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text("รายการค่าใช้จ่าย")))));
            hRow.AppendChild(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text("จำนวนเงิน (บาท)")))));
            table.AppendChild(hRow);

            // Template Row for dynamic expansion
            var dRow = new TableRow();
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{items.id}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{items.name}}")))));
            dRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("{{items.amount}}")))));
            table.AppendChild(dRow);

            body.AppendChild(table);

            body.AppendChild(new Paragraph(
                new Run(new RunProperties(new Bold()), new Text("รวมเป็นเงินทั้งสิ้น: ")),
                new Run(new RunProperties(new Bold(), new Color { Val = "0369A1" }), new Text("{{totalPayableAmount:currency}} บาท ({{totalPayableAmount:thai_baht_text}})")),
                new Run(new Text(""))));

            body.AppendChild(new Paragraph(new Run(new Text("     หากท่านมีข้อสงสัยหรือประสงค์เลื่อนกำหนดการ กรุณาติดต่อเจ้าหน้าที่โอนกรรมสิทธิ์ {{officerPhone}}"))));

            // Signatures
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                new Run(new Text("ขอแสดงความนับถือ"))));
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                new Run(new Text("บริษัท สัมมากร จำกัด (มหาชน)"))));
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                new Run(new RunProperties(new Bold()), new Text("({{officerName}})"))));
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                new Run(new Text("ผู้จัดการฝ่ายโอนกรรมสิทธิ์และบริการลูกค้า"))));

            // Barcode
            body.AppendChild(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new Text("{{barcode:noticeNo}}"))));

            mainPart.Document = new Document(body);
            mainPart.Document.Save();
        }

        // 2. Create JSON payload
        var payloadObj = new
        {
            noticeNo = "SMK-TRF-2026/0199",
            issueDate = "2026-10-18",
            customerName = "นายวิศรุต รัตนภักดี",
            customerAddress = "99/12 หมู่ที่ 5 ตำบลบางกร่าง อำเภอเมืองนนทบุรี จังหวัดนนทบุรี 11000",
            projectName = "สัมมากร ชัยพฤกษ์ - วงแหวน",
            unitNo = "B-14",
            houseModel = "Barn House Type A",
            titleDeedNo = "104928",
            landOfficeName = "จังหวัดนนทบุรี สาขาบางบัวทอง",
            transferDate = "2026-11-15",
            totalPayableAmount = "7,985,400",
            officerName = "นายกิตติศักดิ์ เจริญกิจพาณิชย์",
            officerPhone = "02-123-4567 ต่อ 881",
            items = new[]
            {
                new { id = "1", name = "ยอดเงินคงเหลือชำระวันโอนกรรมสิทธิ์", amount = "7,900,000" },
                new { id = "2", name = "ค่าธรรมเนียมการจดทะเบียนสิทธิและนิติกรรม (ฝ่ายละครึ่ง)", amount = "42,000" },
                new { id = "3", name = "ค่าบริการสาธารณะส่วนกลางล่วงหน้า 2 ปี (35 บาท/ตร.ว./เดือน)", amount = "54,936" },
                new { id = "4", name = "ค่าประกันมิเตอร์ไฟฟ้า ขนาด 30/100A", amount = "6,000" },
                new { id = "5", name = "ค่าประกันมิเตอร์น้ำประปา ขนาด 1/2 นิ้ว", amount = "2,464" }
            }
        };

        var jsonString = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, jsonString);

        // 3. Test render via DocxTemplateEngine
        var mediaService = new MediaGenerationService(new QrCodeService(), new BarcodeService(), new Mock<IImageOptimizer>().Object);
        var mediaInjector = new WordMediaInjector(mediaService);
        var docxEngine = new DocxTemplateEngine(new Mock<IPdfRenderer>().Object, mediaInjector);

        using var templateStream = File.OpenRead(docxPath);
        var renderedBytes = await docxEngine.RenderAsync(templateStream, jsonString, OutputFormat.Docx);

        var outputDocxPath = Path.Combine(_outputDir, "output-06-sales-handover-letter.docx");
        await File.WriteAllBytesAsync(outputDocxPath, renderedBytes);

        renderedBytes.Should().NotBeNullOrEmpty();
        renderedBytes.Length.Should().BeGreaterThan(1000);
        File.Exists(outputDocxPath).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAndVerify_ExcelTemplate_DownPaymentSchedule()
    {
        Directory.CreateDirectory(_outputDir);
        var xlsxPath = Path.Combine(_outputDir, "07-finance-downpayment-plan.xlsx");
        var jsonPath = Path.Combine(_outputDir, "07-finance-downpayment-plan.json");

        // 1. Build .xlsx template using ClosedXML
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("PaymentSchedule");

            // Page Setup A4
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;

            // Title block
            ws.Cell("A1").Value = "บริษัท สัมมากร จำกัด (มหาชน)";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;
            ws.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#0369A1");

            ws.Cell("A2").Value = "ตารางคำนวณราคาแปลงที่ดิน และแผนการผ่อนชำระเงินดาวน์ (FINANCIAL PLAN)";
            ws.Cell("A2").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.FontSize = 11;

            // Metadata info
            ws.Cell("A4").Value = "ชื่อโครงการ:";
            ws.Cell("B4").Value = "{{projectName}}";
            ws.Cell("A4").Style.Font.Bold = true;

            ws.Cell("C4").Value = "แปลงเลขที่:";
            ws.Cell("D4").Value = "{{unitNo}}";
            ws.Cell("C4").Style.Font.Bold = true;

            ws.Cell("A5").Value = "ชื่อลูกค้า:";
            ws.Cell("B5").Value = "{{customerName}}";
            ws.Cell("A5").Style.Font.Bold = true;

            ws.Cell("C5").Value = "แบบบ้าน:";
            ws.Cell("D5").Value = "{{houseModel}}";
            ws.Cell("C5").Style.Font.Bold = true;

            ws.Cell("A6").Value = "ขนาดที่ดิน (ตร.ว.):";
            ws.Cell("B6").Value = "{{landArea}}";
            ws.Cell("A6").Style.Font.Bold = true;

            ws.Cell("C6").Value = "ราคาขายสุทธิ (บาท):";
            ws.Cell("D6").Value = "{{sellingPrice}}";
            ws.Cell("C6").Style.Font.Bold = true;
            ws.Cell("D6").Style.Font.FontColor = XLColor.FromHtml("#0369A1");

            // Schedule Table Header
            ws.Cell("A8").Value = "งวดที่";
            ws.Cell("B8").Value = "กำหนดชำระ";
            ws.Cell("C8").Value = "รายการชำระ";
            ws.Cell("D8").Value = "จำนวนเงิน (บาท)";

            var headerRange = ws.Range("A8:D8");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Template Row for dynamic table expander
            ws.Cell("A9").Value = "{{installments.no}}";
            ws.Cell("B9").Value = "{{installments.dueDate}}";
            ws.Cell("C9").Value = "{{installments.title}}";
            ws.Cell("D9").Value = "{{installments.amount}}";

            var dataRange = ws.Range("A9:D9");
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Total row below template
            ws.Cell("C10").Value = "รวมเงินดาวน์ทั้งสิ้น:";
            ws.Cell("C10").Style.Font.Bold = true;
            ws.Cell("D10").FormulaA1 = "=SUM(D9:D9)";
            ws.Cell("D10").Style.Font.Bold = true;
            ws.Cell("D10").Style.Font.FontColor = XLColor.FromHtml("#15803D");

            // Adjust column widths
            ws.Column(1).Width = 10;
            ws.Column(2).Width = 18;
            ws.Column(3).Width = 35;
            ws.Column(4).Width = 20;

            wb.SaveAs(xlsxPath);
        }

        // 2. Create JSON payload
        var payloadObj = new
        {
            projectName = "สัมมากร ชัยพฤกษ์ - วงแหวน",
            unitNo = "B-14",
            customerName = "นายวิศรุต รัตนภักดี",
            houseModel = "Barn House Type A",
            landArea = "65.4 ตร.ว.",
            sellingPrice = "8,400,000",
            installments = new[]
            {
                new { no = "1", dueDate = "15/10/2026", title = "เงินดาวน์ งวดที่ 1", amount = "50000" },
                new { no = "2", dueDate = "15/11/2026", title = "เงินดาวน์ งวดที่ 2", amount = "50000" },
                new { no = "3", dueDate = "15/12/2026", title = "เงินดาวน์ งวดที่ 3", amount = "50000" },
                new { no = "4", dueDate = "15/01/2027", title = "เงินดาวน์ งวดที่ 4", amount = "50000" },
                new { no = "5", dueDate = "15/02/2027", title = "เงินดาวน์ งวดที่ 5", amount = "50000" },
                new { no = "6", dueDate = "15/03/2027", title = "เงินดาวน์ งวดที่ 6", amount = "50000" }
            }
        };

        var jsonString = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, jsonString);

        // 3. Test render via ExcelTemplateEngine
        var excelMediaService = new MediaGenerationService(new QrCodeService(), new BarcodeService(), new Mock<IImageOptimizer>().Object);
        var mediaInjector = new ExcelMediaInjector(excelMediaService);
        var excelEngine = new ExcelTemplateEngine(new Mock<IPdfRenderer>().Object, mediaInjector);

        using var templateStream = File.OpenRead(xlsxPath);
        var renderedBytes = await excelEngine.RenderAsync(templateStream, jsonString, OutputFormat.Xlsx);

        var outputXlsxPath = Path.Combine(_outputDir, "output-07-finance-downpayment-plan.xlsx");
        await File.WriteAllBytesAsync(outputXlsxPath, renderedBytes);

        renderedBytes.Should().NotBeNullOrEmpty();
        renderedBytes.Length.Should().BeGreaterThan(1000);
        File.Exists(outputXlsxPath).Should().BeTrue();
    }
}
