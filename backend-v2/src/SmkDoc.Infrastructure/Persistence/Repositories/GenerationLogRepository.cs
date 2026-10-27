using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class GenerationLogRepository(AppDbContext context) : IGenerationLogRepository
{
    private readonly AppDbContext _context = context;

    public async Task<GenerationLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.GenerationLogs
            .FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task AddAsync(GenerationLog log, CancellationToken ct = default)
    {
        await _context.GenerationLogs.AddAsync(log, ct);
    }

    public async Task<(IReadOnlyList<GenerationLog> Items, int TotalCount)> ListPagedAsync(
        string? callerApp,
        int page,
        int limit,
        CancellationToken ct = default)
    {
        var query = _context.GenerationLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(callerApp))
        {
            query = query.Where(l => l.CallerApp == callerApp);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
