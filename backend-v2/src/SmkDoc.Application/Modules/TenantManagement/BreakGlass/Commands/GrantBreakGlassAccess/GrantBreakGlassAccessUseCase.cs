using SmkDoc.Application.Modules.TenantManagement.BreakGlass.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.GrantBreakGlassAccess;

public sealed class GrantBreakGlassAccessUseCase(
    IProjectRepository projectRepo,
    IUserRepository userRepo,
    IBreakGlassGrantRepository grantRepo,
    IUnitOfWork unitOfWork,
    ISecurityAuditService securityAuditService,
    TimeProvider timeProvider) : IUseCase<GrantBreakGlassAccessCommand, BreakGlassGrantResultDto>
{
    public async Task<BreakGlassGrantResultDto> ExecuteAsync(GrantBreakGlassAccessCommand command, CancellationToken ct)
    {
        var projectId = new ProjectId(command.TargetProjectId);
        _ = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException("Project", command.TargetProjectId);

        var targetAdminId = new UserId(command.SuperAdminUserId);
        var targetAdmin = await userRepo.GetByIdAsync(targetAdminId, ct)
            ?? throw new NotFoundException("User", command.SuperAdminUserId);

        if (!targetAdmin.IsActive || targetAdmin.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Break-glass grants may only be issued for active SuperAdmin accounts.");
        }

        var granterId = new UserId(command.GrantedByUserId);
        var granter = await userRepo.GetByIdAsync(granterId, ct)
            ?? throw new NotFoundException("User", command.GrantedByUserId);

        if (!granter.IsActive || granter.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Only active SuperAdmin accounts may issue break-glass grants.");
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(command.DurationMinutes);
        var grant = BreakGlassGrant.Issue(projectId, targetAdminId, granterId, command.Reason, now, expiresAt);

        await grantRepo.AddAsync(grant, ct);
        await unitOfWork.CommitAsync(ct);

        securityAuditService.LogBreakGlassAccessGranted(grant.Id.Value, grant.TargetProjectId.Value, grant.SuperAdminUserId.Value, grant.ExpiresAt);

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
