using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a validated data connection name.
/// </summary>
public sealed record class ConnectionName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private ConnectionName(string value) => Value = value;

    public static ConnectionName Create(string? value) =>
        new(Guard.NotBlank(value, "Data connection name", MaxLength));

    public override string ToString() => Value;

    public static implicit operator string(ConnectionName name) => name.Value;
}
