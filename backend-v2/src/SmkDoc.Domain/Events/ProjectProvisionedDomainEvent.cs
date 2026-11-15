using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when a new tenant project is provisioned.
/// </summary>
public sealed record class ProjectProvisionedDomainEvent(
    ProjectId ProjectId,
    CompanyId CompanyId,
    string Slug,
    DateTimeOffset OccurredOn) : IDomainEvent;
