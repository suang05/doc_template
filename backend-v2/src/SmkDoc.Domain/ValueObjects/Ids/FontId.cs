namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for Font aggregate boundary.
/// </summary>
public readonly record struct FontId(Guid Value) : IComparable<FontId>
{
    public static FontId New() => new(Guid.CreateVersion7());
    public static FontId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(FontId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static explicit operator Guid(FontId id) => id.Value;
    public static explicit operator FontId(Guid value) => new(value);

    public static bool operator ==(FontId left, Guid right) => left.Value == right;
    public static bool operator !=(FontId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, FontId right) => left == right.Value;
    public static bool operator !=(Guid left, FontId right) => left != right.Value;
    public static bool operator <(FontId left, FontId right) => left.CompareTo(right) < 0;
    public static bool operator <=(FontId left, FontId right) => left.CompareTo(right) <= 0;
    public static bool operator >(FontId left, FontId right) => left.CompareTo(right) > 0;
    public static bool operator >=(FontId left, FontId right) => left.CompareTo(right) >= 0;
}
