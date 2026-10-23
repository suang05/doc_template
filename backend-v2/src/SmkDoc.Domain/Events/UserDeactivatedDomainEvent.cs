using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when a system user account is deactivated.
/// </summary>
public sealed record class UserDeactivatedDomainEvent(
    UserId UserId,
    DateTimeOffset OccurredOn) : IDomainEvent;
