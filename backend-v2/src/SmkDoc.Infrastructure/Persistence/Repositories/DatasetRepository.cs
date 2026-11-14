using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class DatasetRepository(AppDbContext context) : IDatasetRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Dataset?> GetByIdAsync(DatasetId id, CancellationToken ct)
    {
        return await _context.Datasets
            .Include(d => d.DataConnection)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<IReadOnlyList<Dataset>> GetByIdsAsync(IEnumerable<DatasetId> ids, CancellationToken ct)
    {
        var idSet = ids.ToHashSet();
        if (idSet.Count == 0)
        {
            return [];
        }

        return await _context.Datasets
            .AsNoTracking()
            .Include(d => d.DataConnection)
            .Where(d => idSet.Contains(d.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken ct)
    {
        return await _context.Datasets
            .AsNoTracking()
            .Include(d => d.DataConnection)
            .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Dataset dataset, CancellationToken ct)
    {
        await _context.Datasets.AddAsync(dataset, ct);
    }

    public void Update(Dataset dataset)
    {
        _context.Datasets.Update(dataset);
    }

    public void Remove(Dataset dataset)
    {
        _context.Datasets.Remove(dataset);
    }
}
