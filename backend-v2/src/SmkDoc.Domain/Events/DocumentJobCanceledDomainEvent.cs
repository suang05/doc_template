using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when an asynchronous Pipeline C document job is canceled.
/// </summary>
public sealed record class DocumentJobCanceledDomainEvent(
    DocumentJobId JobId,
    ProjectId ProjectId,
    Uri? WebhookUrl,
    DateTimeOffset OccurredOn) : IDomainEvent;
