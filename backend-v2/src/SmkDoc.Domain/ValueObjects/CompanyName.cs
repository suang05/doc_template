using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing an organization / company name.
/// </summary>
public sealed record class CompanyName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private CompanyName(string value) => Value = value;

    public static CompanyName Create(string? value) =>
        new(Guard.NotBlank(value, "Company name", MaxLength));

    public override string ToString() => Value;

    public static implicit operator string(CompanyName name) => name.Value;
}
