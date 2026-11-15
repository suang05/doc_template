using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when a new template aggregate is created.
/// </summary>
public sealed record class TemplateCreatedDomainEvent(
    TemplateId TemplateId,
    ProjectId ProjectId,
    string Slug,
    DateTimeOffset OccurredOn) : IDomainEvent;
