using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(ProjectId id, CancellationToken ct);
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<Project?> GetFirstForCompanyAsync(CompanyId companyId, CancellationToken ct);
    Task<CompanyId> GetOrCreateDefaultCompanyIdAsync(DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<Project>> ListByIdsAsync(IEnumerable<ProjectId> ids, CancellationToken ct);
    Task AddAsync(Project project, CancellationToken ct);
    void Update(Project project);
}
