using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="GenerationLog"/> entities.
/// </summary>
public interface IGenerationLogRepository
{
    Task<GenerationLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(GenerationLog log, CancellationToken ct = default);
    Task<(IReadOnlyList<GenerationLog> Items, int TotalCount)> ListPagedAsync(
        string? callerApp,
        int page,
        int limit,
        CancellationToken ct = default);
}
