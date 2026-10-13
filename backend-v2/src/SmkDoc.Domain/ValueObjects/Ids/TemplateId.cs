namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for Template aggregate boundary.
/// Zero heap allocation, stack-allocated, immutable.
/// </summary>
public readonly record struct TemplateId(Guid Value) : IComparable<TemplateId>
{
    public static TemplateId New() => new(Guid.CreateVersion7());
    public static TemplateId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(TemplateId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(TemplateId id) => id.Value;
    public static implicit operator TemplateId(Guid value) => new(value);

    public static bool operator ==(TemplateId left, Guid right) => left.Value == right;
    public static bool operator !=(TemplateId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, TemplateId right) => left == right.Value;
    public static bool operator !=(Guid left, TemplateId right) => left != right.Value;
}
