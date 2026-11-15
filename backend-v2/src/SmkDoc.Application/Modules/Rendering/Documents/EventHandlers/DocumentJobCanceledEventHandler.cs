using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.Rendering.Documents.EventHandlers;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain event handler naming convention.")]
public sealed class DocumentJobCanceledEventHandler(
    IWebhookDispatcher webhookDispatcher) : IDomainEventHandler<DocumentJobCanceledDomainEvent>
{
    public Task HandleAsync(DocumentJobCanceledDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return DocumentJobWebhookPayloadBuilder.DispatchIfConfiguredAsync(
            webhookDispatcher,
            domainEvent.ProjectId,
            domainEvent.JobId,
            domainEvent.WebhookUrl,
            "document_job.canceled",
            new
            {
                @event = "document_job.canceled",
                jobId = domainEvent.JobId.Value,
                projectId = domainEvent.ProjectId.Value,
                occurredOn = domainEvent.OccurredOn
            },
            ct);
    }
}
