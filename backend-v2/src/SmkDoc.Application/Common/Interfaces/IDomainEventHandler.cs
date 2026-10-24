using SmkDoc.Domain.Common;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Application handler interface for reacting to domain events.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
