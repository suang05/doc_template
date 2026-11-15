using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when a time-boxed SuperAdmin break-glass grant is issued for a tenant project.
/// </summary>
public sealed record class BreakGlassGrantedDomainEvent(
    BreakGlassGrantId GrantId,
    ProjectId TargetProjectId,
    UserId SuperAdminUserId,
    UserId GrantedByUserId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset OccurredOn) : IDomainEvent;
