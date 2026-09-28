using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public class GenerationLogMetricsRepository(AppDbContext context) : IGenerationLogMetricsRepository
{
    public async Task<LogMetricsDto> GetMetricsAsync(DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken ct = default)
    {
        var query = context.GenerationLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(l => l.CreatedAt >= startDate.Value);
        
        if (endDate.HasValue)
            query = query.Where(l => l.CreatedAt <= endDate.Value);

        var total = await query.CountAsync(ct);
        var successful = await query.CountAsync(l => l.Status == "SUCCESS", ct);
        var failed = await query.CountAsync(l => l.Status != "SUCCESS", ct);
        var avgDuration = total > 0 ? await query.AverageAsync(l => l.DurationMs, ct) : 0;
        var totalBytes = await query.SumAsync(l => l.FileSizeBytes ?? 0, ct);

        var topApps = await query
            .Where(l => l.CallerApp != null)
            .GroupBy(l => l.CallerApp)
            .Select(g => new AppUsageMetricDto(g.Key!, g.Count()))
            .OrderByDescending(x => x.GenerationCount)
            .Take(10)
            .ToListAsync(ct);

        return new LogMetricsDto(
            total,
            successful,
            failed,
            avgDuration,
            totalBytes,
            topApps);
    }
}
