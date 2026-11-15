using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when a previously deactivated tenant project is reactivated.
/// </summary>
public sealed record class ProjectActivatedDomainEvent(
    ProjectId ProjectId,
    DateTimeOffset OccurredOn) : IDomainEvent;
