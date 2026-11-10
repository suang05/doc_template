using ClosedXML.Excel;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Excel;
using SmkDoc.Infrastructure.Imaging;
using SmkDoc.Infrastructure.Parsing;

namespace SmkDoc.IntegrationTests.Generators;

[Trait("Category", "Generator")]
public sealed class ExcelFixtureTemplateTests
{
    private static ExcelTemplateEngine CreateEngine()
    {
        var qr = Substitute.For<IQrCodeService>();
        var bc = Substitute.For<IBarcodeService>();
        var opt = Substitute.For<IImageOptimizer>();
        var pdf = Substitute.For<IPdfRenderer>();
        opt.Optimize(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>())
           .Returns(callInfo => callInfo.Arg<byte[]>());

        var mediaService = new MediaGenerationService(qr, bc, opt);
        var mediaInjector = new ExcelMediaInjector(mediaService);
        return new ExcelTemplateEngine(
            pdf,
            mediaInjector,
            new ExcelTableExpander(),
            new JsonTemplateDataParser());
    }

    [Fact]
    public async Task RenderAsync_WithDocTestFixtureTemplateXlsx_ShouldRenderAndExpandSuccessfully()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures/Templates/template.xlsx"));
        if (!File.Exists(fixturePath))
        {
            fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Fixtures/Templates/template.xlsx"));
        }
        if (!File.Exists(fixturePath))
        {
            fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Fixtures/Templates/template.xlsx"));
        }

        File.Exists(fixturePath).Should().BeTrue($"Fixture template should exist at {fixturePath}");
        var payloadPath = Path.Combine(Path.GetDirectoryName(fixturePath)!, "payload.json");
        File.Exists(payloadPath).Should().BeTrue($"Payload should exist at {payloadPath}");

        string json = await File.ReadAllTextAsync(payloadPath);
        using var templateStream = File.OpenRead(fixturePath);

        var engine = CreateEngine();
        await using var resultStream = await engine.RenderStreamAsync(templateStream, json, OutputFormat.Xlsx);

        resultStream.Should().NotBeNull();
        resultStream.Length.Should().BeGreaterThan(0);
        using var wb = new XLWorkbook(resultStream);

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
