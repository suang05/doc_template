using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.Common;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Application handler interface for reacting to domain events.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Follows DDD domain event handler naming convention rather than a System.EventHandler delegate.")]
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
