using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

public interface IUserProjectRoleRepository
{
    Task<IReadOnlyList<UserProjectRole>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<UserProjectRole>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task<UserProjectRole?> GetAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<int> CountAdminsAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(UserProjectRole role, CancellationToken ct = default);
    void Update(UserProjectRole role);
    void Remove(UserProjectRole role);
}
