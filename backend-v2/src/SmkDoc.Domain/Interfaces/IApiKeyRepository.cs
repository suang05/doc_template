using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKey>> ListAsync(Guid? projectId = null, CancellationToken ct = default);
    Task AddAsync(ApiKey key, CancellationToken ct = default);
    void Update(ApiKey key);
}
