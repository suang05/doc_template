using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.RevokeBreakGlassAccess;

public sealed class RevokeBreakGlassAccessUseCase(
    IBreakGlassGrantRepository grantRepo,
    IUnitOfWork unitOfWork,
    ISecurityAuditService securityAuditService,
    TimeProvider timeProvider) : IUseCase<RevokeBreakGlassAccessCommand>
{
    public async Task ExecuteAsync(RevokeBreakGlassAccessCommand command, CancellationToken ct)
    {
        var grantId = new BreakGlassGrantId(command.GrantId);
        var grant = await grantRepo.GetByIdAsync(grantId, ct)
            ?? throw new NotFoundException("BreakGlassGrant", command.GrantId);

        if (grant.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        grant.Revoke(timeProvider.GetUtcNow());
        grantRepo.Update(grant);
        await unitOfWork.CommitAsync(ct);

        securityAuditService.LogBreakGlassAccessRevoked(grant.Id.Value, grant.TargetProjectId.Value);
    }
}
