namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for Dataset aggregate boundary.
/// Zero heap allocation, stack-allocated, immutable.
/// </summary>
public readonly record struct DatasetId(Guid Value) : IComparable<DatasetId>
{
    public static DatasetId New() => new(Guid.CreateVersion7());
    public static DatasetId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(DatasetId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static explicit operator Guid(DatasetId id) => id.Value;
    public static explicit operator DatasetId(Guid value) => new(value);

    public static bool operator ==(DatasetId left, Guid right) => left.Value == right;
    public static bool operator !=(DatasetId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, DatasetId right) => left == right.Value;
    public static bool operator !=(Guid left, DatasetId right) => left != right.Value;
}
