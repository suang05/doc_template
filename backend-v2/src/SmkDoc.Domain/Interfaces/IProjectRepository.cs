using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Project?> GetBySlugAsync(TemplateSlug slug, CancellationToken ct = default);
    Task<bool> ExistsBySlugAsync(TemplateSlug slug, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListActiveAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<Project> Items, int TotalCount)> ListPagedByUserAsync(
        Guid userId,
        bool isSuperAdmin,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
    void Update(Project project);
}
