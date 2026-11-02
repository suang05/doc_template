using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.Rendering.Documents.EventHandlers;

public sealed class DocumentVersionCreatedEventHandler(
    ISecurityAuditService securityAuditService) : IDomainEventHandler<DocumentVersionCreatedDomainEvent>
{
    public Task HandleAsync(DocumentVersionCreatedDomainEvent domainEvent, CancellationToken ct = default)
    {
        securityAuditService.LogDocumentVersionCreated(
            domainEvent.ProjectId.Value,
            domainEvent.DocumentId.Value,
            domainEvent.Version,
            domainEvent.GenerationLogId);
        return Task.CompletedTask;
    }
}
