namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for DocumentJob aggregate boundary.
/// </summary>
public readonly record struct DocumentJobId(Guid Value) : IComparable<DocumentJobId>
{
    public static DocumentJobId New() => new(Guid.CreateVersion7());
    public static DocumentJobId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(DocumentJobId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static explicit operator Guid(DocumentJobId id) => id.Value;
    public static explicit operator DocumentJobId(Guid value) => new(value);

    public static bool operator ==(DocumentJobId left, Guid right) => left.Value == right;
    public static bool operator !=(DocumentJobId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, DocumentJobId right) => left == right.Value;
    public static bool operator !=(Guid left, DocumentJobId right) => left != right.Value;
    public static bool operator <(DocumentJobId left, DocumentJobId right) => left.CompareTo(right) < 0;
    public static bool operator <=(DocumentJobId left, DocumentJobId right) => left.CompareTo(right) <= 0;
    public static bool operator >(DocumentJobId left, DocumentJobId right) => left.CompareTo(right) > 0;
    public static bool operator >=(DocumentJobId left, DocumentJobId right) => left.CompareTo(right) >= 0;
}
