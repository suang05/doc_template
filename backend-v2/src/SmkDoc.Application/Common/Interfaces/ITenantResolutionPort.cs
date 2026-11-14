using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Read-only perimeter port resolving active project status and user membership claims
/// without coupling HTTP middleware to persistence entities or repositories.
/// </summary>
public interface ITenantResolutionPort
{
    Task<ResolvedProjectState?> FindProjectAsync(ProjectId projectId, CancellationToken ct);
    Task<ResolvedProjectMembership?> FindMembershipAsync(ProjectId projectId, UserId userId, CancellationToken ct);
}
