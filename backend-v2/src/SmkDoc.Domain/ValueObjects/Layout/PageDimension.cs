using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.ValueObjects.Layout;

/// <summary>
/// Lightweight, zero-allocation 8-byte page dimension representation (width and height in millimeters).
/// Suitable for high-throughput Gotenberg Chromium / PDF and ClosedXML page setup pipelines.
/// </summary>
public readonly record struct PageDimension
{
    public float WidthMm { get; }
    public float HeightMm { get; }

    public static PageDimension A4 => new(210f, 297f);
    public static PageDimension A3 => new(297f, 420f);
    public static PageDimension Letter => new(215.9f, 279.4f);

    public PageDimension(float widthMm, float heightMm)
    {
        if (widthMm <= 0 || float.IsNaN(widthMm) || float.IsInfinity(widthMm))
        {
            throw new DomainValidationException("Page width must be a positive finite number.", "PAGE_WIDTH_INVALID");
        }

        if (heightMm <= 0 || float.IsNaN(heightMm) || float.IsInfinity(heightMm))
        {
            throw new DomainValidationException("Page height must be a positive finite number.", "PAGE_HEIGHT_INVALID");
        }

        WidthMm = widthMm;
        HeightMm = heightMm;
    }

    public bool IsPortrait => HeightMm >= WidthMm;
    public bool IsLandscape => WidthMm > HeightMm;

    public PageDimension ToPortrait() => IsPortrait ? this : new(HeightMm, WidthMm);
    public PageDimension ToLandscape() => IsLandscape ? this : new(HeightMm, WidthMm);

    public override string ToString() => $"{WidthMm}mm x {HeightMm}mm ({(IsPortrait ? "Portrait" : "Landscape")})";
}
