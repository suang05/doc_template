namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for Project aggregate boundary.
/// Zero heap allocation, stack-allocated, immutable.
/// </summary>
public readonly record struct ProjectId(Guid Value) : IComparable<ProjectId>
{
    public static ProjectId New() => new(Guid.CreateVersion7());
    public static ProjectId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(ProjectId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static explicit operator Guid(ProjectId id) => id.Value;
    public static explicit operator ProjectId(Guid value) => new(value);

    public static bool operator ==(ProjectId left, Guid right) => left.Value == right;
    public static bool operator !=(ProjectId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, ProjectId right) => left == right.Value;
    public static bool operator !=(Guid left, ProjectId right) => left != right.Value;
}
