using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when a rendered output artifact is persisted and recorded.
/// </summary>
public sealed record class DocumentRenderedDomainEvent(
    Guid GenerationLogId,
    ProjectId ProjectId,
    TemplateId? TemplateId,
    string OutputFormat,
    string OutputKey,
    long OutputSizeBytes,
    DateTimeOffset OccurredOn) : IDomainEvent;
