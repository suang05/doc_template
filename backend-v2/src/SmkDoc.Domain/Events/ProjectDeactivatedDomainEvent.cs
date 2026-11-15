using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when a tenant project is deactivated and suspended from data-plane execution.
/// </summary>
public sealed record class ProjectDeactivatedDomainEvent(
    ProjectId ProjectId,
    DateTimeOffset OccurredOn) : IDomainEvent;
