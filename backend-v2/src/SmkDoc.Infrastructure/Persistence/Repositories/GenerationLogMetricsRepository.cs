using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class GenerationLogMetricsRepository(AppDbContext context) : IGenerationLogMetricsRepository
{
    public async Task<LogMetricsDto> GetMetricsAsync(
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken ct = default)
    {
        var query = context.GenerationLogs.AsNoTracking();

        if (startDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= endDate.Value);
        }

        var summary = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Successful = g.Count(l => l.Status == GenerationStatus.Success),
                Failed = g.Count(l => l.Status != GenerationStatus.Success),
                AvgDuration = g.Average(l => (double?)l.DurationMs) ?? 0d,
                TotalBytes = g.Sum(l => l.FileSizeBytes ?? 0L)
            })
            .FirstOrDefaultAsync(ct);

        var topApps = await query
            .Where(l => l.CallerApp != null)
            .GroupBy(l => l.CallerApp)
            .Select(g => new AppUsageMetricDto(g.Key!, g.Count()))
            .OrderByDescending(x => x.GenerationCount)
            .Take(10)
            .ToListAsync(ct);

        return new LogMetricsDto(
            summary?.Total ?? 0,
            summary?.Successful ?? 0,
            summary?.Failed ?? 0,
            summary?.AvgDuration ?? 0d,
            summary?.TotalBytes ?? 0L,
            topApps);
    }
}
