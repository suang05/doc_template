using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when an API key is revoked.
/// </summary>
public sealed record class ApiKeyRevokedDomainEvent(
    ApiKeyId ApiKeyId,
    ProjectId ProjectId,
    DateTimeOffset OccurredOn) : IDomainEvent;
