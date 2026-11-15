namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for ProjectQuota aggregate boundary.
/// </summary>
public readonly record struct ProjectQuotaId(Guid Value) : IComparable<ProjectQuotaId>
{
    public static ProjectQuotaId New() => new(Guid.CreateVersion7());
    public static ProjectQuotaId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(ProjectQuotaId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static explicit operator Guid(ProjectQuotaId id) => id.Value;
    public static explicit operator ProjectQuotaId(Guid value) => new(value);

    public static bool operator ==(ProjectQuotaId left, Guid right) => left.Value == right;
    public static bool operator !=(ProjectQuotaId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, ProjectQuotaId right) => left == right.Value;
    public static bool operator !=(Guid left, ProjectQuotaId right) => left != right.Value;
    public static bool operator <(ProjectQuotaId left, ProjectQuotaId right) => left.CompareTo(right) < 0;
    public static bool operator <=(ProjectQuotaId left, ProjectQuotaId right) => left.CompareTo(right) <= 0;
    public static bool operator >(ProjectQuotaId left, ProjectQuotaId right) => left.CompareTo(right) > 0;
    public static bool operator >=(ProjectQuotaId left, ProjectQuotaId right) => left.CompareTo(right) >= 0;
}
