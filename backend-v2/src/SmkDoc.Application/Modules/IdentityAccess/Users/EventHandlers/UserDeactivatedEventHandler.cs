using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.EventHandlers;

/// <summary>
/// Domain event handler reacting to user account deactivations by immediately revoking
/// all active refresh tokens and recording the security audit trail.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Follows DDD domain event handler naming convention rather than a System.EventHandler delegate.")]
public sealed class UserDeactivatedEventHandler(
    IRefreshTokenRepository refreshTokenRepo,
    ISecurityAuditService securityAuditService) : IDomainEventHandler<UserDeactivatedDomainEvent>
{
    public async Task HandleAsync(UserDeactivatedDomainEvent domainEvent, CancellationToken ct)
    {
        securityAuditService.LogUserDeactivated(domainEvent.UserId.Value);
        await refreshTokenRepo.RevokeAllByUserIdAsync(domainEvent.UserId, domainEvent.OccurredOn, ct);
    }
}
