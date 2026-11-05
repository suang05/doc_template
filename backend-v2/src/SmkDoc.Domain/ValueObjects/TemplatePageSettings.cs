using System.Text.Json.Serialization;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value object defining paper geometry, orientation, margins, and header/footer definitions for templates.
/// Defaults to standard A4 portrait with 15mm top/bottom and 10mm left/right margins.
/// </summary>
public sealed record TemplatePageSettings(
    [property: JsonPropertyName("paperSize")] string PaperSize = "A4",
    [property: JsonPropertyName("orientation")] string Orientation = "portrait",
    [property: JsonPropertyName("marginTopMm")] double MarginTopMm = 15.0,
    [property: JsonPropertyName("marginBottomMm")] double MarginBottomMm = 15.0,
    [property: JsonPropertyName("marginLeftMm")] double MarginLeftMm = 10.0,
    [property: JsonPropertyName("marginRightMm")] double MarginRightMm = 10.0,
    [property: JsonPropertyName("showPageNumber")] bool ShowPageNumber = true,
    [property: JsonPropertyName("headerHtml")] string? HeaderHtml = null,
    [property: JsonPropertyName("footerHtml")] string? FooterHtml = null)
{
    public static TemplatePageSettings Default => new();

    public string ToCssAtPageRule() =>
        $"@page {{ size: {PaperSize} {Orientation}; margin: {MarginTopMm}mm {MarginRightMm}mm {MarginBottomMm}mm {MarginLeftMm}mm; }}";
}

