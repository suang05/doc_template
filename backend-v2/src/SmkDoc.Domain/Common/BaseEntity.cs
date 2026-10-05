namespace SmkDoc.Domain.Common;

/// <summary>
/// Abstract base class for all Domain Entities.
/// Provides UUIDv7 primary identity, audit timestamps, and entity equality based on identity.
/// </summary>
public abstract class BaseEntity : IEquatable<BaseEntity>
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; protected set; }

    protected BaseEntity() { }

    protected BaseEntity(Guid? id, DateTimeOffset? createdAt = null)
    {
        if (id.HasValue && id.Value != Guid.Empty)
        {
            Id = id.Value;
        }

        if (createdAt.HasValue)
        {
            CreatedAt = createdAt.Value;
        }
    }

    protected void SetUpdated(DateTimeOffset? updatedAt = null) =>
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;

    public override bool Equals(object? obj) =>
        obj is BaseEntity other && Equals(other);

    public bool Equals(BaseEntity? other)
    {
        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        if (Id == Guid.Empty || other.Id == Guid.Empty)
        {
            return false;
        }

        return Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(BaseEntity? left, BaseEntity? right)
    {
        if (left is null ^ right is null)
        {
            return false;
        }

        return left is null || left.Equals(right);
    }

    public static bool operator !=(BaseEntity? left, BaseEntity? right) => !(left == right);
}
