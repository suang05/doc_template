using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

/// <summary>
/// Infrastructure adapter implementing <see cref="ITenantResolutionPort"/> for Tier 1 perimeter resolution.
/// </summary>
public sealed class TenantResolutionAdapter(
    IProjectRepository projectRepository,
    IUserProjectRoleRepository userProjectRoleRepository) : ITenantResolutionPort
{
    public async Task<ResolvedProjectState?> FindProjectAsync(ProjectId projectId, CancellationToken ct)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        return project is null
            ? null
            : new ResolvedProjectState(project.Id.Value, project.IsActive);
    }

    public async Task<ResolvedProjectMembership?> FindMembershipAsync(ProjectId projectId, UserId userId, CancellationToken ct)
    {
        var membership = await userProjectRoleRepository.GetAsync(projectId, userId, ct);
        return membership is null
            ? null
            : new ResolvedProjectMembership(membership.ProjectId.Value, membership.UserId.Value, membership.Role.Name);
    }
}
