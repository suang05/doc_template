namespace SmkDoc.Tests.Domain.ValueObjects;

public sealed class TemplatePageSettingsTests
{
    [Fact]
    public void Default_ShouldHaveStandardA4PortraitMetrics()
    {
        var settings = TemplatePageSettings.Default;

        settings.PaperSize.Should().Be("A4");
        settings.Orientation.Should().Be("portrait");
        settings.MarginTopMm.Should().Be(15.0);
        settings.MarginBottomMm.Should().Be(15.0);
        settings.MarginLeftMm.Should().Be(10.0);
        settings.MarginRightMm.Should().Be(10.0);
        settings.ShowPageNumber.Should().BeTrue();
        settings.HeaderHtml.Should().BeNull();
        settings.FooterHtml.Should().BeNull();
    }

    [Fact]
    public void ToCssAtPageRule_ShouldRenderValidCssPagedMediaRule()
    {
        var settings = new TemplatePageSettings(
            PaperSize: "A4",
            Orientation: "landscape",
            MarginTopMm: 20,
            MarginBottomMm: 20,
            MarginLeftMm: 12,
            MarginRightMm: 12);

        var css = settings.ToCssAtPageRule();

        css.Should().Be("@page { size: A4 landscape; margin: 20mm 12mm 20mm 12mm; }");
    }
}

