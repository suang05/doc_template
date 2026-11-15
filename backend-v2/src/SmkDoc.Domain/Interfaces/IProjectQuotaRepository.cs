using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface IProjectQuotaRepository
{
    Task<ProjectQuota?> GetByProjectIdAsync(ProjectId projectId, CancellationToken ct);
    Task AddAsync(ProjectQuota quota, CancellationToken ct);
    void Update(ProjectQuota quota);
}
