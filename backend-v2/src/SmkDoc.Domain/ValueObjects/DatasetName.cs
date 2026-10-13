using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a validated dataset name.
/// </summary>
public sealed record class DatasetName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private DatasetName(string value) => Value = value;

    public static DatasetName Create(string? value) =>
        new(Guard.NotBlank(value, "Dataset name", MaxLength));

    public override string ToString() => Value;

    public static implicit operator string(DatasetName name) => name.Value;
}
