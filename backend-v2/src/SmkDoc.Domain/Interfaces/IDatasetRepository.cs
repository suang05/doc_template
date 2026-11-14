using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="Dataset"/> aggregate roots.
/// </summary>
public interface IDatasetRepository
{
    Task<Dataset?> GetByIdAsync(DatasetId id, CancellationToken ct);
    Task<IReadOnlyList<Dataset>> GetByIdsAsync(IEnumerable<DatasetId> ids, CancellationToken ct);
    Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken ct);
    Task AddAsync(Dataset dataset, CancellationToken ct);
    void Update(Dataset dataset);
    void Remove(Dataset dataset);
}
