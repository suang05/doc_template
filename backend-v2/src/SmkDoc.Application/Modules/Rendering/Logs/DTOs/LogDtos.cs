using System.Diagnostics.CodeAnalysis;

namespace SmkDoc.Application.Modules.Rendering.Logs.DTOs;

/// <summary>
/// DTO representing an execution log entry for document generation.
/// </summary>
public sealed record GenerationLogDto(
    Guid Id,
    Guid? TemplateId,
    Guid? TemplateVersionId,
    Guid? ApiKeyId,
    string? CallerApp,
    string? TriggerSource,
    string? OutputFormat,
    long? FileSizeBytes,
    int? PageCount,
    int DurationMs,
    string Status,
    string? ErrorMsg,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Aggregated usage and performance metrics for document generations.
/// </summary>
public sealed record LogMetricsDto(
    int TotalGenerations,
    int SuccessfulGenerations,
    int FailedGenerations,
    double AverageDurationMs,
    long TotalFileSizeBytes,
    IEnumerable<AppUsageMetricDto> TopCallerApps
);

/// <summary>
/// Metric entry for per-application generation count.
/// </summary>
public sealed record AppUsageMetricDto(
    string CallerApp,
    int GenerationCount
);

/// <summary>
/// Paged result envelope for generation logs query.
/// </summary>
public record GenerationLogPagedResultDto(
    IEnumerable<GenerationLogDto> Logs,
    int Total,
    int Page,
    int Limit
);

public sealed record GenerationLogPagedResult(
    IEnumerable<GenerationLogDto> Logs,
    int Total,
    int Page,
    int Limit
) : GenerationLogPagedResultDto(Logs, Total, Page, Limit);

/// <summary>
/// Pre-signed document download URL and validity window.
/// </summary>
public sealed record DownloadUrlResultDto(
    [param: SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "JSON/OpenAPI wire DTO contract (supports relative API paths and pre-signed URLs).")]
    string Url,
    int ExpiresInSeconds = 3600)
{
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON/OpenAPI wire DTO contract (supports relative API paths and pre-signed URLs).")]
    public string Url { get; init; } = Url;
}
