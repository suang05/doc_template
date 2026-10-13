using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.ValueObjects.Layout;

/// <summary>
/// Lightweight, zero-allocation 16-byte page margin representation (top, bottom, left, right in millimeters).
/// Suitable for high-throughput Gotenberg Chromium / PDF and ClosedXML page setup pipelines.
/// </summary>
public readonly record struct PageMargin
{
    public float TopMm { get; }
    public float BottomMm { get; }
    public float LeftMm { get; }
    public float RightMm { get; }

    public static PageMargin Normal => new(20f, 20f, 20f, 20f);
    public static PageMargin Narrow => new(12.7f, 12.7f, 12.7f, 12.7f);
    public static PageMargin None => new(0f, 0f, 0f, 0f);

    public PageMargin(float topMm, float bottomMm, float leftMm, float rightMm)
    {
        if (topMm < 0 || float.IsNaN(topMm) || float.IsInfinity(topMm))
        {
            throw new DomainValidationException("Top margin must be a non-negative finite number.", "PAGE_MARGIN_INVALID");
        }

        if (bottomMm < 0 || float.IsNaN(bottomMm) || float.IsInfinity(bottomMm))
        {
            throw new DomainValidationException("Bottom margin must be a non-negative finite number.", "PAGE_MARGIN_INVALID");
        }

        if (leftMm < 0 || float.IsNaN(leftMm) || float.IsInfinity(leftMm))
        {
            throw new DomainValidationException("Left margin must be a non-negative finite number.", "PAGE_MARGIN_INVALID");
        }

        if (rightMm < 0 || float.IsNaN(rightMm) || float.IsInfinity(rightMm))
        {
            throw new DomainValidationException("Right margin must be a non-negative finite number.", "PAGE_MARGIN_INVALID");
        }

        TopMm = topMm;
        BottomMm = bottomMm;
        LeftMm = leftMm;
        RightMm = rightMm;
    }

    public static PageMargin Uniform(float marginMm) => new(marginMm, marginMm, marginMm, marginMm);

    public override string ToString() => $"Margins(Top: {TopMm}mm, Bottom: {BottomMm}mm, Left: {LeftMm}mm, Right: {RightMm}mm)";
}
