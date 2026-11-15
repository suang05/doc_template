using SmkDoc.Application.Modules.TenantManagement.Quotas.DTOs;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.TenantManagement.Quotas.Commands.UpsertProjectQuota;

public sealed class UpsertProjectQuotaUseCase(
    IProjectRepository projectRepo,
    IProjectQuotaRepository quotaRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<UpsertProjectQuotaCommand, ProjectQuotaResultDto>
{
    public async Task<ProjectQuotaResultDto> ExecuteAsync(UpsertProjectQuotaCommand command, CancellationToken ct)
    {
        var projectId = new ProjectId(command.ProjectId);
        _ = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException("Project", command.ProjectId);

        var now = timeProvider.GetUtcNow();
        var quota = await quotaRepo.GetByProjectIdAsync(projectId, ct);

        if (quota is null)
        {
            if (command.ExpectedVersion != 1)
            {
                throw PreconditionFailedException.VersionMismatch();
            }

            quota = ProjectQuota.Create(
                projectId,
                command.MaxRequestsPerMinute,
                command.MaxConcurrentJobs,
                command.MaxBatchItems,
                command.MaxStorageBytes,
                command.MaxActiveApiKeys,
                now);

            if (!command.IsActive)
            {
                quota.Deactivate(now);
            }

            await quotaRepo.AddAsync(quota, ct);
        }
        else
        {
            if (quota.Version != command.ExpectedVersion)
            {
                throw PreconditionFailedException.VersionMismatch();
            }

            quota.UpdateLimits(
                command.MaxRequestsPerMinute,
                command.MaxConcurrentJobs,
                command.MaxBatchItems,
                command.MaxStorageBytes,
                command.MaxActiveApiKeys,
                now);

            if (command.IsActive)
            {
                quota.Activate(now);
            }
            else
            {
                quota.Deactivate(now);
            }

            quotaRepo.Update(quota);
        }

        await unitOfWork.CommitAsync(ct);

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
