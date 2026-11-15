using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Events;

namespace SmkDoc.Infrastructure.Events;

/// <summary>
/// Emits durable SIEM security audit logs whenever SuperAdmin break-glass grants are issued or revoked.
/// </summary>
public sealed class BreakGlassAuditHandler(
    ISecurityAuditService securityAuditService) :
    IDomainEventHandler<BreakGlassGrantedDomainEvent>,
    IDomainEventHandler<BreakGlassRevokedDomainEvent>
{
    public Task HandleAsync(BreakGlassGrantedDomainEvent domainEvent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        securityAuditService.LogBreakGlassAccessGranted(
            domainEvent.GrantId.Value,
            domainEvent.TargetProjectId.Value,
            domainEvent.SuperAdminUserId.Value,
            domainEvent.ExpiresAt);
        return Task.CompletedTask;
    }

    public Task HandleAsync(BreakGlassRevokedDomainEvent domainEvent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        securityAuditService.LogBreakGlassAccessRevoked(
            domainEvent.GrantId.Value,
            domainEvent.TargetProjectId.Value);
        return Task.CompletedTask;
    }
}
