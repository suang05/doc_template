using SmkDoc.Application.Modules.TenantManagement.BreakGlass.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.GetBreakGlassGrantById;

public sealed class GetBreakGlassGrantByIdUseCase(
    IBreakGlassGrantRepository grantRepo,
    TimeProvider timeProvider) : IUseCase<GetBreakGlassGrantByIdQuery, BreakGlassGrantResultDto>
{
    public async Task<BreakGlassGrantResultDto> ExecuteAsync(GetBreakGlassGrantByIdQuery query, CancellationToken ct)
    {
        var grant = await grantRepo.GetByIdAsync(new BreakGlassGrantId(query.GrantId), ct)
            ?? throw new NotFoundException("BreakGlassGrant", query.GrantId);

        var now = timeProvider.GetUtcNow();
        return new BreakGlassGrantResultDto(
            grant.Id.Value,
            grant.TargetProjectId.Value,
            grant.SuperAdminUserId.Value,
            grant.GrantedByUserId.Value,
            grant.Reason,
            grant.GrantedAt,
            grant.ExpiresAt,
            grant.RevokedAt,
            grant.IsActiveAt(now),
            grant.Version);
    }
}
