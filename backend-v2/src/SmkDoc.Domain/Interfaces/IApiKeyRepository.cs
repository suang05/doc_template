using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiKey?> GetByIdAsync(Guid id, Guid projectId, CancellationToken ct = default);

    /// <summary>Returns the active key matching the hash, or null.</summary>
    Task<ApiKey?> GetByKeyHashAsync(Sha256Hash keyHash, CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(Guid projectId, ApiKeyName name, CancellationToken ct = default);
    Task AddAsync(ApiKey key, CancellationToken ct = default);
    void Update(ApiKey key);
}
