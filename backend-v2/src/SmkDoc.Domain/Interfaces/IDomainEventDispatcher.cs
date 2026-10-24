using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain event dispatcher port for publishing domain lifecycle events post-commit.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchEventsAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default);
}
