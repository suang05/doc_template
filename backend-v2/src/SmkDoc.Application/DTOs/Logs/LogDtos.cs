using System;
using System.Collections.Generic;

namespace SmkDoc.Application.DTOs.Logs;

/// <summary>
/// DTO representing an execution log entry for document generation.
/// </summary>
public record GenerationLogDto(
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
public record LogMetricsDto(
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
public record AppUsageMetricDto(
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

public record GenerationLogPagedResult(
    IEnumerable<GenerationLogDto> Logs,
    int Total,
    int Page,
    int Limit
) : GenerationLogPagedResultDto(Logs, Total, Page, Limit);
