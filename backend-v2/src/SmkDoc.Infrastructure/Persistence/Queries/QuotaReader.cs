using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class QuotaReader(AppDbContext context) : IQuotaReader
{
    private static readonly ProjectQuotaSnapshot DefaultSnapshot = new(
        ProjectQuota.DefaultMaxRequestsPerMinute,
        ProjectQuota.DefaultMaxConcurrentJobs,
        ProjectQuota.DefaultMaxBatchItems,
        ProjectQuota.DefaultMaxStorageBytes,
        ProjectQuota.DefaultMaxActiveApiKeys);

    public async Task<ProjectQuotaSnapshot> GetQuotaForProjectAsync(ProjectId projectId, CancellationToken ct)
    {
        var quota = await context.ProjectQuotas
            .AsNoTracking()
            .Where(q => q.ProjectId == projectId && q.IsActive)
            .Select(q => new ProjectQuotaSnapshot(
                q.MaxRequestsPerMinute,
                q.MaxConcurrentJobs,
                q.MaxBatchItems,
                q.MaxStorageBytes,
                q.MaxActiveApiKeys))
            .FirstOrDefaultAsync(ct);

        return quota ?? DefaultSnapshot;
    }
}
