using ClosedXML.Excel;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Excel;
using SmkDoc.Infrastructure.Imaging;
using Xunit;

namespace SmkDoc.IntegrationTests.Generators;

[Trait("Category", "Generator")]
public class ExcelFixtureTemplateTests
{
    private static ExcelTemplateEngine CreateEngine()
    {
        var qr = new Mock<IQrCodeService>();
        var bc = new Mock<IBarcodeService>();
        var opt = new Mock<IImageOptimizer>();
        var pdf = new Mock<IPdfRenderer>();
        opt.Setup(x => x.Optimize(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
           .Returns<byte[], int, int>((b, _, _) => b);

        var mediaService = new MediaGenerationService(qr.Object, bc.Object, opt.Object);
        var mediaInjector = new ExcelMediaInjector(mediaService);
        return new ExcelTemplateEngine(pdf.Object, mediaInjector);
    }

    [Fact]
    public async Task RenderAsync_WithDocTestFixtureTemplateXlsx_ShouldRenderAndExpandSuccessfully()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../docs/test-fixtures/template.xlsx"));
        if (!File.Exists(fixturePath))
        {
            fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/test-fixtures/template.xlsx"));
        }

        File.Exists(fixturePath).Should().BeTrue($"Fixture template should exist at {fixturePath}");
        var payloadPath = Path.Combine(Path.GetDirectoryName(fixturePath)!, "payload.json");
        File.Exists(payloadPath).Should().BeTrue($"Payload should exist at {payloadPath}");

        string json = await File.ReadAllTextAsync(payloadPath);
        using var templateStream = File.OpenRead(fixturePath);

        var engine = CreateEngine();
        var result = await engine.RenderAsync(templateStream, json, OutputFormat.Xlsx);

        result.Should().NotBeNullOrEmpty();
        using var wb = new XLWorkbook(new MemoryStream(result));

        // 1. Sheet 1 - Main Info
        var wsMain = wb.Worksheet("ข้อมูลหลัก");
        wsMain.Cell("B4").GetString().Should().Be("บริษัท สมาคมก่อสร้างไทย จำกัด (มหาชน)");
        wsMain.Cell("B13").GetString().Should().Be("นายสมชาย ใจดี");

        // 2. Sheet 2 - Items Table expansion
        var wsItems = wb.Worksheet("รายการ (Table)");
        wsItems.Cell("A3").GetString().Should().Be("1");
        wsItems.Cell("B3").GetString().Should().Be("งานรื้อถอนอาคารเดิมและเตรียมพื้นที่");
        wsItems.Cell("A4").GetString().Should().Be("2");
        wsItems.Cell("B4").GetString().Should().Be("งานฐานรากและโครงสร้างคอนกรีตเสริมเหล็กชั้นใต้ดิน");

        // 3. Sheet 4 - Milestone expansion
        var wsMilestones = wb.Worksheet("Milestone");
        wsMilestones.Cell("A3").GetString().Should().Be("Phase 1");
        wsMilestones.Cell("B3").GetString().Should().Be("งานฐานราก");
        wsMilestones.Cell("A4").GetString().Should().Be("Phase 2");
        wsMilestones.Cell("B4").GetString().Should().Be("งานโครงสร้าง");
    }
}
