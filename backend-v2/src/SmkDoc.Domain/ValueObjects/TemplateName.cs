using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a validated template name.
/// </summary>
public sealed record class TemplateName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private TemplateName(string value) => Value = value;

    public static TemplateName Create(string? value) =>
        new(Guard.NotBlank(value, "Template name", MaxLength));

    public override string ToString() => Value;

    public static implicit operator string(TemplateName name) => name.Value;
}
