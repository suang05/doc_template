using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class DataConnectionRepository(AppDbContext context) : IDataConnectionRepository
{
    private readonly AppDbContext _context = context;

    public async Task<DataConnection?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var connectionId = new DataConnectionId(id);
        return await _context.DataConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId, ct);
    }

    public async Task<IReadOnlyList<DataConnection>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idSet = ids.Select(id => new DataConnectionId(id)).ToHashSet();
        return await _context.DataConnections
            .Where(c => idSet.Contains(c.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DataConnection>> ListAsync(CancellationToken ct = default)
    {
        return await _context.DataConnections
            .AsNoTracking()
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(DataConnection connection, CancellationToken ct = default)
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
