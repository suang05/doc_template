using SmkDocServer.Domain.Entities;

namespace SmkDocServer.Domain.Interfaces;

public interface IGenerationLogService
{
    /// <summary>
    /// Records a document generation job into PostgreSQL for audit tracking.
    /// </summary>
    Task RecordLogAsync(GenerationLog log);

    /// <summary>
    /// Queries recent generation audit logs with optional pagination and filters.
    /// </summary>
    Task<(List<GenerationLog> Logs, int TotalCount)> GetRecentLogsAsync(int page = 1, int pageSize = 50, Guid? projectId = null, string? status = null);

    /// <summary>
    /// Retrieves a single generation log by its ID. Returns null if not found.
    /// </summary>
    Task<GenerationLog?> GetByIdAsync(Guid logId);

    /// <summary>
    /// Retrieves aggregated metrics (Total generated, Success rate, Avg duration, etc.)
    /// </summary>
    Task<GenerationMetrics> GetMetricsAsync();
}

public class GenerationMetrics
{
    public int TotalGenerations { get; set; }
    public int TotalSuccess { get; set; }
    public int TotalFailed { get; set; }
    public double SuccessRatePercentage { get; set; }
    public double AvgExecutionTimeMs { get; set; }
}
