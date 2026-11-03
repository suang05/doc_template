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

    public async Task AddRangeAsync(IEnumerable<GenerationLog> logs, CancellationToken ct = default)
    {
        await _context.GenerationLogs.AddRangeAsync(logs, ct);
    }
}
