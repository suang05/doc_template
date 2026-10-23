namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for Company aggregate boundary.
/// Zero heap allocation, stack-allocated, immutable.
/// </summary>
public readonly record struct CompanyId(Guid Value) : IComparable<CompanyId>
{
    public static CompanyId New() => new(Guid.CreateVersion7());
    public static CompanyId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(CompanyId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static explicit operator Guid(CompanyId id) => id.Value;
    public static explicit operator CompanyId(Guid value) => new(value);

    public static bool operator ==(CompanyId left, Guid right) => left.Value == right;
    public static bool operator !=(CompanyId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, CompanyId right) => left == right.Value;
    public static bool operator !=(Guid left, CompanyId right) => left != right.Value;
}
