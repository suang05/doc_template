using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListActiveAsync(CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
    void Update(Project project);
}
