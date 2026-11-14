using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="GenerationLog"/> entities.
/// </summary>
public interface IGenerationLogRepository
{
    Task<GenerationLog?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(GenerationLog log, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<GenerationLog> logs, CancellationToken ct);
    Task<IReadOnlyList<GenerationLog>> GetExpiredLogsWithOutputsAsync(DateTimeOffset cutoffTime, int maxBatchSize, CancellationToken ct);
}
