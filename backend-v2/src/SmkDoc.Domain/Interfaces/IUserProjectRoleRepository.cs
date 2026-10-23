using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface IUserProjectRoleRepository
{
    Task<IReadOnlyList<UserProjectRole>> ListByProjectAsync(ProjectId projectId, CancellationToken ct = default);
    Task<IReadOnlyList<UserProjectRole>> ListByUserAsync(UserId userId, CancellationToken ct = default);
    Task<UserProjectRole?> GetAsync(ProjectId projectId, UserId userId, CancellationToken ct = default);
    Task<int> CountAdminsAsync(ProjectId projectId, CancellationToken ct = default);
    Task AddAsync(UserProjectRole role, CancellationToken ct = default);
    void Update(UserProjectRole role);
    void Remove(UserProjectRole role);
}
