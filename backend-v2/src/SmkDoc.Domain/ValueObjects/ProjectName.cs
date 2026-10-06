using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a project / tenant name.
/// </summary>
public sealed class ProjectName : ValueObject
{
    public const int MaxLength = 100;

    public string Value { get; }

    private ProjectName(string value) => Value = value;

    public static ProjectName Create(string? value) =>
        new(Guard.NotBlank(value, "Project name", MaxLength));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(ProjectName name) => name.Value;
}
