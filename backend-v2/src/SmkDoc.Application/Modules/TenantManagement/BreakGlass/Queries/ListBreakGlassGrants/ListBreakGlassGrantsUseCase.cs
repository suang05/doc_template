using SmkDoc.Application.Modules.TenantManagement.BreakGlass.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.ListBreakGlassGrants;

public sealed class ListBreakGlassGrantsUseCase(
    IBreakGlassGrantRepository grantRepo,
    TimeProvider timeProvider) : IUseCase<ListBreakGlassGrantsQuery, IReadOnlyList<BreakGlassGrantResultDto>>
{
    public async Task<IReadOnlyList<BreakGlassGrantResultDto>> ExecuteAsync(ListBreakGlassGrantsQuery query, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        UserId? superAdminId = query.SuperAdminUserId.HasValue && query.SuperAdminUserId.Value != Guid.Empty
            ? new UserId(query.SuperAdminUserId.Value)
            : null;
        ProjectId? projectId = query.TargetProjectId.HasValue && query.TargetProjectId.Value != Guid.Empty
            ? new ProjectId(query.TargetProjectId.Value)
            : null;

        var grants = await grantRepo.ListActiveGrantsAsync(superAdminId, projectId, now, ct);

        return grants.Select(g => new BreakGlassGrantResultDto(
            g.Id.Value,
            g.TargetProjectId.Value,
            g.SuperAdminUserId.Value,
            g.GrantedByUserId.Value,
            g.Reason,
            g.GrantedAt,
            g.ExpiresAt,
            g.RevokedAt,
            g.IsActiveAt(now),
            g.Version)).ToList();
    }
}
