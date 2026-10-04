using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiKey?> GetByIdAsync(Guid id, Guid projectId, CancellationToken ct = default);
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKey>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(ApiKey key, CancellationToken ct = default);
    void Update(ApiKey key);
}
