using System.Text.RegularExpressions;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a normalized dataset alias (used in {{alias.field}} placeholders).
/// </summary>
public sealed partial class DatasetAlias : ValueObject
{
    public const int MaxLength = 50;
    private static readonly Regex AliasRegex = GeneratedAliasRegex();

    public string Value { get; }

    private DatasetAlias(string value) => Value = value;

    public static DatasetAlias Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Dataset alias cannot be empty or whitespace.");
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            throw new DomainValidationException($"Dataset alias must not exceed {MaxLength} characters.");
        }

        if (!AliasRegex.IsMatch(normalized))
        {
            throw new DomainValidationException(
                $"Dataset alias '{value}' is invalid. Only lowercase letters, digits, hyphens, and underscores are allowed.");
        }

        return new DatasetAlias(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string?(DatasetAlias? alias) => alias?.Value;

    [GeneratedRegex(@"^[a-z0-9_-]{1,50}$", RegexOptions.Compiled)]
    private static partial Regex GeneratedAliasRegex();
}
