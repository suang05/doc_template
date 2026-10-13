namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for DataConnection aggregate boundary.
/// Zero heap allocation, stack-allocated, immutable.
/// </summary>
public readonly record struct DataConnectionId(Guid Value) : IComparable<DataConnectionId>
{
    public static DataConnectionId New() => new(Guid.CreateVersion7());
    public static DataConnectionId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(DataConnectionId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(DataConnectionId id) => id.Value;
    public static implicit operator DataConnectionId(Guid value) => new(value);

    public static bool operator ==(DataConnectionId left, Guid right) => left.Value == right;
    public static bool operator !=(DataConnectionId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, DataConnectionId right) => left == right.Value;
    public static bool operator !=(Guid left, DataConnectionId right) => left != right.Value;
}
