using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.Rendering.Documents.EventHandlers;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain event handler naming convention.")]
public sealed class DocumentJobSucceededEventHandler(
    IWebhookDispatcher webhookDispatcher) : IDomainEventHandler<DocumentJobSucceededDomainEvent>
{
    public Task HandleAsync(DocumentJobSucceededDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return DocumentJobWebhookPayloadBuilder.DispatchIfConfiguredAsync(
            webhookDispatcher,
            domainEvent.ProjectId,
            domainEvent.JobId,
            domainEvent.WebhookUrl,
            "document_job.succeeded",
            new
            {
                @event = "document_job.succeeded",
                jobId = domainEvent.JobId.Value,
                projectId = domainEvent.ProjectId.Value,
                templateSlug = domainEvent.TemplateSlug,
                outputFormat = domainEvent.OutputFormat,
                resultGenerationLogId = domainEvent.ResultGenerationLogId,
                downloadUrl = $"/api/v1/document-jobs/{domainEvent.JobId.Value}/result",
                occurredOn = domainEvent.OccurredOn
            },
            ct);
    }
}
