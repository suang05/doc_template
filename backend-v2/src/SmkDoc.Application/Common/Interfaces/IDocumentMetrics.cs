namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Port for collecting real-time APM metrics and traces for document generation operations.
/// Pure C# interface with zero dependencies on external monitoring vendors.
/// </summary>
public interface IDocumentMetrics
{
    /// <summary>
    /// Records a successful document generation operation with latency and output size.
    /// </summary>
    void RecordGenerationSuccess(string engineType, string outputFormat, double durationMs, long bytes);

    /// <summary>
    /// Records a failed document generation attempt with latency and failure reason.
    /// </summary>
    void RecordGenerationFailure(string engineType, string outputFormat, double durationMs, string failureReason);

    /// <summary>
    /// Records a compiled template cache hit or miss event.
    /// </summary>
    void RecordCacheRequest(bool isHit);
}
