using SmkDoc.Application.Modules.TenantManagement.Quotas.DTOs;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.TenantManagement.Quotas.Queries.GetProjectQuota;

public sealed class GetProjectQuotaUseCase(
    IProjectRepository projectRepo,
    IProjectQuotaRepository quotaRepo,
    TimeProvider timeProvider) : IUseCase<GetProjectQuotaQuery, ProjectQuotaResultDto>
{
    public async Task<ProjectQuotaResultDto> ExecuteAsync(GetProjectQuotaQuery query, CancellationToken ct)
    {
        var projectId = new ProjectId(query.ProjectId);
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException("Project", query.ProjectId);

        var quota = await quotaRepo.GetByProjectIdAsync(projectId, ct);
        if (quota is null)
        {
            return new ProjectQuotaResultDto(
                Guid.Empty,
                project.Id.Value,
                ProjectQuota.DefaultMaxRequestsPerMinute,
                ProjectQuota.DefaultMaxConcurrentJobs,
                ProjectQuota.DefaultMaxBatchItems,
                ProjectQuota.DefaultMaxStorageBytes,
                ProjectQuota.DefaultMaxActiveApiKeys,
                true,
                timeProvider.GetUtcNow(),
                null,
                Version: 1);
        }

        return new ProjectQuotaResultDto(
            quota.Id.Value,
            quota.ProjectId.Value,
            quota.MaxRequestsPerMinute,
            quota.MaxConcurrentJobs,
            quota.MaxBatchItems,
            quota.MaxStorageBytes,
            quota.MaxActiveApiKeys,
            quota.IsActive,
            quota.CreatedAt,
            quota.UpdatedAt,
            quota.Version);
    }
}
