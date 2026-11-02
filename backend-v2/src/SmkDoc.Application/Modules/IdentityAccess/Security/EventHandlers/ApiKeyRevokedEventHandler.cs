using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.EventHandlers;

public sealed class ApiKeyRevokedEventHandler(
    ISecurityAuditService securityAuditService) : IDomainEventHandler<ApiKeyRevokedDomainEvent>
{
    public Task HandleAsync(ApiKeyRevokedDomainEvent domainEvent, CancellationToken ct = default)
    {
        securityAuditService.LogApiKeyRevoked(
            domainEvent.ProjectId.Value,
            domainEvent.ApiKeyId.Value);
        return Task.CompletedTask;
    }
}
