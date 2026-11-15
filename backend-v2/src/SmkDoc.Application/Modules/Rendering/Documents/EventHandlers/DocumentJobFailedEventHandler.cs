using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.Rendering.Documents.EventHandlers;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain event handler naming convention.")]
public sealed class DocumentJobFailedEventHandler(
    IWebhookDispatcher webhookDispatcher) : IDomainEventHandler<DocumentJobFailedDomainEvent>
{
    public Task HandleAsync(DocumentJobFailedDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return DocumentJobWebhookPayloadBuilder.DispatchIfConfiguredAsync(
            webhookDispatcher,
            domainEvent.ProjectId,
            domainEvent.JobId,
            domainEvent.WebhookUrl,
            "document_job.failed",
            new
            {
                @event = "document_job.failed",
                jobId = domainEvent.JobId.Value,
                projectId = domainEvent.ProjectId.Value,
                templateSlug = domainEvent.TemplateSlug,
                outputFormat = domainEvent.OutputFormat,
                errorCode = domainEvent.ErrorCode,
                errorMessage = domainEvent.ErrorMessage,
                occurredOn = domainEvent.OccurredOn
            },
            ct);
    }
}
