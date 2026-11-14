using System.Globalization;
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

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(TId id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    public virtual string GetConcurrencyToken() =>
        (UpdatedAt ?? CreatedAt).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

    public virtual string? GetAlternateConcurrencyToken() => null;
}

