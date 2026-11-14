using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class GenerationLogRepository(AppDbContext context) : IGenerationLogRepository
{
    private readonly AppDbContext _context = context;

    public async Task<GenerationLog?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.GenerationLogs
            .FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task AddAsync(GenerationLog log, CancellationToken ct)
    {
        await _context.GenerationLogs.AddAsync(log, ct);
    }

    public async Task AddRangeAsync(IEnumerable<GenerationLog> logs, CancellationToken ct)
    {
        await _context.GenerationLogs.AddRangeAsync(logs, ct);
    }

    public async Task<IReadOnlyList<GenerationLog>> GetExpiredLogsWithOutputsAsync(DateTimeOffset cutoffTime, int maxBatchSize, CancellationToken ct)
    {
#pragma warning disable RS0030 // [PHASE-3-TECH-DEBT] Migrate retention cleanup batch query to explicit retention RLS role
        return await _context.GenerationLogs
            .IgnoreQueryFilters()
            .Where(l => l.OutputKey != null && l.OutputKey != "" && l.CreatedAt <= cutoffTime)
            .OrderBy(l => l.CreatedAt)
            .Take(maxBatchSize)
            .ToListAsync(ct);
#pragma warning restore RS0030
    }
}
