using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.EventHandlers;

public sealed class UserDeactivatedEventHandler(
    ISecurityAuditService securityAuditService) : IDomainEventHandler<UserDeactivatedDomainEvent>
{
    public Task HandleAsync(UserDeactivatedDomainEvent domainEvent, CancellationToken ct = default)
    {
        securityAuditService.LogUserDeactivated(domainEvent.UserId.Value);
        return Task.CompletedTask;
    }
}
