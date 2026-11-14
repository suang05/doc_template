using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="DataConnection"/> aggregate roots.
/// </summary>
public interface IDataConnectionRepository
{
    Task<DataConnection?> GetByIdAsync(DataConnectionId id, CancellationToken ct);
    Task<IReadOnlyList<DataConnection>> GetByIdsAsync(IEnumerable<DataConnectionId> ids, CancellationToken ct);
    Task<IReadOnlyList<DataConnection>> ListAsync(CancellationToken ct);
    Task AddAsync(DataConnection connection, CancellationToken ct);
    void Update(DataConnection connection);
    void Remove(DataConnection connection);
}
