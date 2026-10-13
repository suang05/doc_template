namespace SmkDoc.Domain.ValueObjects.Ids;

/// <summary>
/// Strongly-typed 16-byte identifier for ApiKey aggregate boundary.
/// Zero heap allocation, stack-allocated, immutable.
/// </summary>
public readonly record struct ApiKeyId(Guid Value) : IComparable<ApiKeyId>
{
    public static ApiKeyId New() => new(Guid.CreateVersion7());
    public static ApiKeyId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(ApiKeyId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(ApiKeyId id) => id.Value;
    public static implicit operator ApiKeyId(Guid value) => new(value);

    public static bool operator ==(ApiKeyId left, Guid right) => left.Value == right;
    public static bool operator !=(ApiKeyId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, ApiKeyId right) => left == right.Value;
    public static bool operator !=(Guid left, ApiKeyId right) => left != right.Value;
}
