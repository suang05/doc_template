using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a validated dataset SQL query.
/// </summary>
public sealed record class SqlQuery
{
    public const int MaxQueryLength = 100_000;

    public string Value { get; }

    private SqlQuery(string value) => Value = value;

    public static SqlQuery Create(string? value) =>
        new(Guard.NotBlank(value, "SQL query", MaxQueryLength));

    public override string ToString() => Value;

    public static implicit operator string(SqlQuery query) => query.Value;
}
