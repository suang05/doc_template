using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface IUserProjectRoleRepository
{
    Task<IReadOnlyList<UserProjectRole>> ListByProjectAsync(ProjectId projectId, CancellationToken ct);
    Task<IReadOnlyList<UserProjectRole>> ListByUserAsync(UserId userId, CancellationToken ct);
    Task<UserProjectRole?> GetAsync(ProjectId projectId, UserId userId, CancellationToken ct);
    Task<int> CountAdminsAsync(ProjectId projectId, CancellationToken ct);
    Task<IReadOnlyList<UserProjectRole>> ListForCurrentProjectAsync(CancellationToken ct);
    Task<UserProjectRole?> GetForCurrentProjectAsync(UserId userId, CancellationToken ct);
    Task<int> CountAdminsForCurrentProjectAsync(CancellationToken ct);
    Task AddAsync(UserProjectRole role, CancellationToken ct);
    void Update(UserProjectRole role);
    void Remove(UserProjectRole role);
}
