namespace SmkDoc.Domain.Common;

/// <summary>
/// Generic abstract base class for Domain Entities with strongly-typed identity.
/// Provides primary identity, audit timestamps, and entity equality based on identity.
/// </summary>
public abstract class BaseEntity<TId> : IHaveDomainEvents, IEquatable<BaseEntity<TId>>
    where TId : struct, IComparable<TId>, IEquatable<TId>
{
    public TId Id { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; protected set; }
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    protected BaseEntity()
    {
    }

    protected BaseEntity(TId id, DateTimeOffset? createdAt = null)
    {
        Id = id;
        if (createdAt.HasValue)
        {
            CreatedAt = createdAt.Value;
        }
    }

    protected void SetUpdated(DateTimeOffset? updatedAt = null) =>
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;

    public override bool Equals(object? obj) =>
        obj is BaseEntity<TId> other && Equals(other);

    public bool Equals(BaseEntity<TId>? other)
    {
        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        if (EqualityComparer<TId>.Default.Equals(Id, default) ||
            EqualityComparer<TId>.Default.Equals(other.Id, default))
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(BaseEntity<TId>? left, BaseEntity<TId>? right)
    {
        if (left is null ^ right is null)
        {
            return false;
        }

        return left is null || left.Equals(right);
    }

    public static bool operator !=(BaseEntity<TId>? left, BaseEntity<TId>? right) => !(left == right);
}

/// <summary>
/// Abstract base class for child Domain Entities with Guid primary identity.
/// Provides UUIDv7 primary identity, audit timestamps, and entity equality based on identity.
/// </summary>
public abstract class BaseEntity : BaseEntity<Guid>
{
    protected BaseEntity()
    {
        Id = Guid.CreateVersion7();
    }

    protected BaseEntity(Guid? id, DateTimeOffset? createdAt = null)
        : base(id.HasValue && id.Value != Guid.Empty ? id.Value : Guid.CreateVersion7(), createdAt)
    {
    }
}
