using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="Dataset"/> aggregate roots.
/// </summary>
public interface IDatasetRepository
{
    Task<Dataset?> GetByIdAsync(DatasetId id, CancellationToken ct = default);
    Task<IReadOnlyList<Dataset>> GetByIdsAsync(IEnumerable<DatasetId> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Dataset dataset, CancellationToken ct = default);
    void Update(Dataset dataset);
    void Remove(Dataset dataset);
}
