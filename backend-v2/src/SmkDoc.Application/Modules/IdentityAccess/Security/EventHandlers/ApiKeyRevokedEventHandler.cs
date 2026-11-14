using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.EventHandlers;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Follows DDD domain event handler naming convention rather than a System.EventHandler delegate.")]
public sealed class ApiKeyRevokedEventHandler(
    ISecurityAuditService securityAuditService) : IDomainEventHandler<ApiKeyRevokedDomainEvent>
{
    public Task HandleAsync(ApiKeyRevokedDomainEvent domainEvent, CancellationToken ct)
    {
        securityAuditService.LogApiKeyRevoked(
            domainEvent.ProjectId.Value,
            domainEvent.ApiKeyId.Value);
        return Task.CompletedTask;
    }
}
