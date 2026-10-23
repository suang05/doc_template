namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for User aggregate boundary.
/// Zero heap allocation, stack-allocated, immutable.
/// </summary>
public readonly record struct UserId(Guid Value) : IComparable<UserId>
{
    public static UserId New() => new(Guid.CreateVersion7());
    public static UserId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(UserId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static explicit operator Guid(UserId id) => id.Value;
    public static explicit operator UserId(Guid value) => new(value);

    public static bool operator ==(UserId left, Guid right) => left.Value == right;
    public static bool operator !=(UserId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, UserId right) => left == right.Value;
    public static bool operator !=(Guid left, UserId right) => left != right.Value;
}
