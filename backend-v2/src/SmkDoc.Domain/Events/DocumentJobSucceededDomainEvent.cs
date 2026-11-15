using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when an asynchronous Pipeline C document job finishes successfully.
/// </summary>
public sealed record class DocumentJobSucceededDomainEvent(
    DocumentJobId JobId,
    ProjectId ProjectId,
    string TemplateSlug,
    string OutputFormat,
    Uri? WebhookUrl,
    string? ResultStorageKey,
    Guid? ResultGenerationLogId,
    DateTimeOffset OccurredOn) : IDomainEvent;
