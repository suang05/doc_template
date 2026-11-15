using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Domain.Common;

/// <summary>
/// Generic abstract base class for Domain Aggregate Roots.
/// Strictly encapsulates domain event management and optimistic concurrency contracts.
/// </summary>
public abstract class AggregateRoot<TId> : BaseEntity<TId>, IHaveDomainEvents, IConcurrencyAware
    where TId : struct, IComparable<TId>, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public long Version { get; protected set; } = 1;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(TId id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
        Version = 1;
    }

    protected override void SetUpdated(DateTimeOffset now)
    {
        base.SetUpdated(now);
        Version++;
    }
}

