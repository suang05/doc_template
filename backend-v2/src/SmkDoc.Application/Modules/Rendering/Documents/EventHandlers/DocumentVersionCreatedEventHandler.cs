using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.Rendering.Documents.EventHandlers;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Follows DDD domain event handler naming convention rather than a System.EventHandler delegate.")]
public sealed class DocumentVersionCreatedEventHandler(
    ISecurityAuditService securityAuditService) : IDomainEventHandler<DocumentVersionCreatedDomainEvent>
{
    public Task HandleAsync(DocumentVersionCreatedDomainEvent domainEvent, CancellationToken ct)
    {
        securityAuditService.LogDocumentVersionCreated(
            domainEvent.ProjectId.Value,
            domainEvent.DocumentId.Value,
            domainEvent.Version,
            domainEvent.GenerationLogId);
        return Task.CompletedTask;
    }
}
