using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when a template version is published as the current active version.
/// </summary>
public sealed record class TemplatePublishedDomainEvent(
    TemplateId TemplateId,
    ProjectId ProjectId,
    Guid VersionId,
    int Version,
    DateTimeOffset OccurredOn) : IDomainEvent;
