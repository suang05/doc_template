using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.EventHandlers;

/// <summary>
/// Domain event handler reacting to user account deactivations by immediately revoking
/// all active refresh tokens and recording the security audit trail.
/// </summary>
public sealed class UserDeactivatedEventHandler(
    IRefreshTokenRepository refreshTokenRepo,
    ISecurityAuditService securityAuditService) : IDomainEventHandler<UserDeactivatedDomainEvent>
{
    public async Task HandleAsync(UserDeactivatedDomainEvent domainEvent, CancellationToken ct = default)
    {
        securityAuditService.LogUserDeactivated(domainEvent.UserId.Value);
        await refreshTokenRepo.RevokeAllByUserIdAsync(domainEvent.UserId, domainEvent.OccurredOn, ct);
    }
}
