using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class ProjectQuotaRepository(AppDbContext context) : IProjectQuotaRepository
{
    public async Task<ProjectQuota?> GetByProjectIdAsync(ProjectId projectId, CancellationToken ct)
    {
        return await context.ProjectQuotas
            .FirstOrDefaultAsync(q => q.ProjectId == projectId, ct);
    }

    public async Task AddAsync(ProjectQuota quota, CancellationToken ct)
    {
        await context.ProjectQuotas.AddAsync(quota, ct);
    }

    public void Update(ProjectQuota quota)
    {
        context.ProjectQuotas.Update(quota);
    }
}
