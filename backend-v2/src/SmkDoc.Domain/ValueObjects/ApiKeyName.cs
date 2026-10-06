using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Human-readable label of an API key. Identity-less; two names are equal when their trimmed values are equal.
/// </summary>
public sealed class ApiKeyName : ValueObject
{
    public const int MaxLength = 100;

    public string Value { get; }

    private ApiKeyName(string value) => Value = value;

    public static ApiKeyName Create(string? value) =>
        new(Guard.NotBlank(value, "API key name", MaxLength));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(ApiKeyName name) => name.Value;
}
