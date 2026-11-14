using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class DataConnectionRepository(AppDbContext context) : IDataConnectionRepository
{
    private readonly AppDbContext _context = context;

    public async Task<DataConnection?> GetByIdAsync(DataConnectionId id, CancellationToken ct)
    {
        return await _context.DataConnections
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<DataConnection>> GetByIdsAsync(IEnumerable<DataConnectionId> ids, CancellationToken ct)
    {
        var idSet = ids.ToHashSet();
        if (idSet.Count == 0)
        {
            return [];
        }

        return await _context.DataConnections
            .AsNoTracking()
            .Where(c => idSet.Contains(c.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DataConnection>> ListAsync(CancellationToken ct)
    {
        return await _context.DataConnections
            .AsNoTracking()
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(DataConnection connection, CancellationToken ct)
    {
        await _context.DataConnections.AddAsync(connection, ct);
    }

    public void Update(DataConnection connection)
    {
        _context.DataConnections.Update(connection);
    }

    public void Remove(DataConnection connection)
    {
        _context.DataConnections.Remove(connection);
    }
}
