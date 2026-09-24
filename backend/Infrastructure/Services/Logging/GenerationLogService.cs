using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Infrastructure.Data;

namespace SmkDocServer.Infrastructure.Services.Logging;

public class GenerationLogService : IGenerationLogService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<GenerationLogService> _logger;

    public GenerationLogService(AppDbContext dbContext, ILogger<GenerationLogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RecordLogAsync(GenerationLog log)
    {
        try
        {
            _dbContext.GenerationLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record generation audit log for Id: {LogId}", log.Id);
            // Do not throw so document delivery to client is not disrupted by audit logging failures
        }
    }

    public async Task<(List<GenerationLog> Logs, int TotalCount)> GetRecentLogsAsync(
        int page = 1, 
        int pageSize = 50, 
        Guid? projectId = null, 
        string? status = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.GenerationLogs
            .Include(l => l.Project)
            .Include(l => l.ApiKey)
            .AsNoTracking()
            .AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(l => l.ProjectId == projectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        int totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (logs, totalCount);
    }

    public async Task<GenerationLog?> GetByIdAsync(Guid logId)
    {
        return await _dbContext.GenerationLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == logId);
    }

    public async Task<GenerationMetrics> GetMetricsAsync()
    {
        var total = await _dbContext.GenerationLogs.CountAsync();
        if (total == 0)
        {
            return new GenerationMetrics
            {
                TotalGenerations = 0,
                TotalSuccess = 0,
                TotalFailed = 0,
                SuccessRatePercentage = 100.0,
                AvgExecutionTimeMs = 0
            };
        }

        var success = await _dbContext.GenerationLogs.CountAsync(l => l.Status == "SUCCESS");
        var failed = total - success;
        var avgDuration = await _dbContext.GenerationLogs
            .Where(l => l.Status == "SUCCESS")
            .Select(l => (double?)l.ExecutionTimeMs)
            .AverageAsync() ?? 0.0;

        return new GenerationMetrics
        {
            TotalGenerations = total,
            TotalSuccess = success,
            TotalFailed = failed,
            SuccessRatePercentage = Math.Round((double)success / total * 100.0, 1),
            AvgExecutionTimeMs = Math.Round(avgDuration, 1)
        };
    }
}
