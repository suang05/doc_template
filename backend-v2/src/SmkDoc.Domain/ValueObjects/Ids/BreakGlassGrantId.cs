namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for BreakGlassGrant aggregate boundary.
/// </summary>
public readonly record struct BreakGlassGrantId(Guid Value) : IComparable<BreakGlassGrantId>
{
    public static BreakGlassGrantId New() => new(Guid.CreateVersion7());
    public static BreakGlassGrantId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(BreakGlassGrantId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static explicit operator Guid(BreakGlassGrantId id) => id.Value;
    public static explicit operator BreakGlassGrantId(Guid value) => new(value);

    public static bool operator ==(BreakGlassGrantId left, Guid right) => left.Value == right;
    public static bool operator !=(BreakGlassGrantId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, BreakGlassGrantId right) => left == right.Value;
    public static bool operator !=(Guid left, BreakGlassGrantId right) => left != right.Value;
    public static bool operator <(BreakGlassGrantId left, BreakGlassGrantId right) => left.CompareTo(right) < 0;
    public static bool operator <=(BreakGlassGrantId left, BreakGlassGrantId right) => left.CompareTo(right) <= 0;
    public static bool operator >(BreakGlassGrantId left, BreakGlassGrantId right) => left.CompareTo(right) > 0;
    public static bool operator >=(BreakGlassGrantId left, BreakGlassGrantId right) => left.CompareTo(right) >= 0;
}
