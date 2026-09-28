using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Application.DTOs;
using SmkDoc.Application.DTOs.Logs;

namespace SmkDoc.Application.UseCases.Logs;

/// <summary>
/// Application Use Case for querying Generation Logs.
/// Encapsulates pagination logic, keeping Controllers thin.
/// </summary>
public sealed class GenerationLogUseCase(
    IRepository<GenerationLog> repo,
    IGenerationLogMetricsRepository metricsRepo)
{
    public async Task<GenerationLogPagedResult> ListAsync(
        int page,
        int limit,
        string? callerApp,
        CancellationToken ct)
    {
        page  = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        var (logs, total) = await repo.PagedListAsync(
            predicate:  string.IsNullOrWhiteSpace(callerApp)
                            ? null
                            : l => l.CallerApp == callerApp,
            orderBy:    l => l.CreatedAt,
            descending: true,
            page:       page,
            limit:      limit,
            ct:         ct);

        var dtos = logs.Select(l => new GenerationLogDto(
            l.Id,
            l.TemplateId,
            l.TemplateVersionId,
            l.ApiKeyId,
            l.CallerApp,
            l.TriggerSource,
            l.OutputFormat?.Extension,
            l.FileSizeBytes,
            l.PageCount,
            l.DurationMs,
            l.Status,
            l.ErrorMsg,
            l.CreatedAt)).ToList();

        return new GenerationLogPagedResult(dtos, total, page, limit);
    }

    public async Task<LogMetricsDto> GetMetricsAsync(DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken ct)
    {
        return await metricsRepo.GetMetricsAsync(startDate, endDate, ct);
    }
}
