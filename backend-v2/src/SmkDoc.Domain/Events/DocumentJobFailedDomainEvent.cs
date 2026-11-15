using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when an asynchronous Pipeline C document job transitions to Failed.
/// </summary>
public sealed record class DocumentJobFailedDomainEvent(
    DocumentJobId JobId,
    ProjectId ProjectId,
    string TemplateSlug,
    string OutputFormat,
    Uri? WebhookUrl,
    string ErrorCode,
    string ErrorMessage,
    DateTimeOffset OccurredOn) : IDomainEvent;
