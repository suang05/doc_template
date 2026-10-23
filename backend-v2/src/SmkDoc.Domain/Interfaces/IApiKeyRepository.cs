using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(ApiKeyId id, CancellationToken ct = default);
    Task<ApiKey?> GetByIdAsync(ApiKeyId id, ProjectId projectId, CancellationToken ct = default);

    /// <summary>Returns the active key matching the hash, or null.</summary>
    Task<ApiKey?> GetByKeyHashAsync(Sha256Hash keyHash, CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> ListByProjectAsync(ProjectId projectId, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(ProjectId projectId, ApiKeyName name, CancellationToken ct = default);
    Task AddAsync(ApiKey key, CancellationToken ct = default);
    void Update(ApiKey key);
}
