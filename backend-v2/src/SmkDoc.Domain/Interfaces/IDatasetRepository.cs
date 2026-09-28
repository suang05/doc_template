using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="Dataset"/> aggregate roots.
/// </summary>
public interface IDatasetRepository
{
    Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Dataset>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<List<Dataset>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Dataset dataset, CancellationToken ct = default);
    void Update(Dataset dataset);
    void Remove(Dataset dataset);
}
