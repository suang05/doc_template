namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Domain parameter record encapsulating performance and execution size metrics for document generation.
/// </summary>
public sealed record GenerationMetrics(
    int DurationMs,
    long? FileSizeBytes = null,
    int? PageCount = null)
{
    public static GenerationMetrics ForDuration(int durationMs, long? fileSizeBytes = 0) => new(durationMs, fileSizeBytes);

    public static GenerationMetrics Of(int durationMs, long fileSizeBytes, int? pageCount = null) =>
        new(durationMs, fileSizeBytes, pageCount);
}
