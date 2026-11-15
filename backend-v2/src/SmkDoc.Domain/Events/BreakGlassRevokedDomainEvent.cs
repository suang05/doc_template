using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when a break-glass grant is explicitly revoked prior to natural expiry.
/// </summary>
public sealed record class BreakGlassRevokedDomainEvent(
    BreakGlassGrantId GrantId,
    ProjectId TargetProjectId,
    UserId SuperAdminUserId,
    DateTimeOffset OccurredOn) : IDomainEvent;
