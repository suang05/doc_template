using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.Authoring.Templates.EventHandlers;

public sealed class TemplatePublishedEventHandler(
    ISecurityAuditService securityAuditService,
    ITemplateCacheInvalidationBus cacheInvalidationBus) : IDomainEventHandler<TemplatePublishedDomainEvent>
{
    public async Task HandleAsync(TemplatePublishedDomainEvent domainEvent, CancellationToken ct = default)
    {
        securityAuditService.LogTemplatePublished(
            domainEvent.ProjectId.Value,
            domainEvent.TemplateId.Value,
            domainEvent.VersionId,
            domainEvent.Version);

        await cacheInvalidationBus.PublishTemplateInvalidationAsync(
            domainEvent.ProjectId,
            domainEvent.TemplateId,
            domainEvent.Version,
            ct);
    }
}
