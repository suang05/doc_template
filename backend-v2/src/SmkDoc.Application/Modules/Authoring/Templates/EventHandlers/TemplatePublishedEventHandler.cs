using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.Authoring.Templates.EventHandlers;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Follows DDD domain event handler naming convention rather than a System.EventHandler delegate.")]
public sealed class TemplatePublishedEventHandler(
    ISecurityAuditService securityAuditService) : IDomainEventHandler<TemplatePublishedDomainEvent>
{
    public Task HandleAsync(TemplatePublishedDomainEvent domainEvent, CancellationToken ct)
    {
        securityAuditService.LogTemplatePublished(
            domainEvent.ProjectId.Value,
            domainEvent.TemplateId.Value,
            domainEvent.VersionId,
            domainEvent.Version);

        return Task.CompletedTask;
    }
}
