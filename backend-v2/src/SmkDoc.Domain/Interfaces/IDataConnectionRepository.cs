using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="DataConnection"/> aggregate roots.
/// </summary>
public interface IDataConnectionRepository
{
    Task<DataConnection?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<DataConnection>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<List<DataConnection>> ListAsync(CancellationToken ct = default);
    Task AddAsync(DataConnection connection, CancellationToken ct = default);
    void Update(DataConnection connection);
    void Remove(DataConnection connection);
}
