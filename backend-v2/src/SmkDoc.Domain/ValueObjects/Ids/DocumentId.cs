namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for Document aggregate boundary.
/// Zero heap allocation, stack-allocated, immutable.
/// </summary>
public readonly record struct DocumentId(Guid Value) : IComparable<DocumentId>
{
    public static DocumentId New() => new(Guid.CreateVersion7());
    public static DocumentId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(DocumentId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static explicit operator Guid(DocumentId id) => id.Value;
    public static explicit operator DocumentId(Guid value) => new(value);

    public static bool operator ==(DocumentId left, Guid right) => left.Value == right;
    public static bool operator !=(DocumentId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, DocumentId right) => left == right.Value;
    public static bool operator !=(Guid left, DocumentId right) => left != right.Value;
}
