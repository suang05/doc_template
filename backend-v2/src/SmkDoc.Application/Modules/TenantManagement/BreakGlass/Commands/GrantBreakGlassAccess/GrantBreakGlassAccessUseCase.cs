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

        var targetAdminId = await EnsureActiveSuperAdminAsync(
            command.SuperAdminUserId,
            "Break-glass grants may only be issued for active SuperAdmin accounts.",
            ct);

        var granterId = await EnsureActiveSuperAdminAsync(
            command.GrantedByUserId,
            "Only active SuperAdmin accounts may issue break-glass grants.",
            ct);

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(command.DurationMinutes);
        var grant = BreakGlassGrant.Issue(projectId, targetAdminId, granterId, command.Reason, now, expiresAt);

        await grantRepo.AddAsync(grant, ct);
        await unitOfWork.CommitAsync(ct);

        securityAuditService.LogBreakGlassAccessGranted(grant.Id.Value, grant.TargetProjectId.Value, grant.SuperAdminUserId.Value, grant.ExpiresAt);

        return BreakGlassGrantResultDto.FromEntity(grant, now);
    }

    private async Task<UserId> EnsureActiveSuperAdminAsync(Guid rawUserId, string forbiddenMessage, CancellationToken ct)
    {
        var userId = new UserId(rawUserId);
        var user = await userRepo.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", rawUserId);

        if (!user.IsActive || user.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException(forbiddenMessage);
        }

        return userId;
    }
}
