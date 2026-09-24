namespace SmkDoc.Application.Common.Models;

public sealed record DocxScanResult(bool IsSafe, IReadOnlyList<string> Threats)
{
    public static DocxScanResult Safe() => new(true, []);
}
