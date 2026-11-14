using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(ApiKeyId id, ProjectId projectId, CancellationToken ct);
    Task<ApiKey?> GetByIdWithoutProjectAsync(ApiKeyId id, CancellationToken ct);

    /// <summary>Returns the active key matching the hash, or null.</summary>
    Task<ApiKey?> GetByKeyHashAsync(Sha256Hash keyHash, CancellationToken ct);

    Task<IReadOnlyList<ApiKey>> ListByProjectAsync(ProjectId projectId, CancellationToken ct);
    Task<IReadOnlyList<ApiKey>> ListCurrentProjectAsync(CancellationToken ct);
    Task<IReadOnlyList<ApiKey>> ListAllAsync(CancellationToken ct);

    Task AddAsync(ApiKey key, CancellationToken ct);
    void Update(ApiKey key);
}
