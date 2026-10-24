using System.Text.RegularExpressions;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a normalized URL-friendly project tenant slug.
/// Ensures immutability, format validation, and structural equality.
/// </summary>
public sealed partial record ProjectSlug
{
    private static readonly Regex SlugRegex = GeneratedSlugRegex();

    public string Value { get; }

    public ProjectSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Project slug cannot be empty or whitespace.");
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length is < 2 or > 100)
        {
            throw new DomainValidationException("Project slug length must be between 2 and 100 characters.");
        }

        if (!SlugRegex.IsMatch(normalized))
        {
            throw new DomainValidationException($"Project slug '{value}' is invalid. Allowed characters are lowercase alphanumeric letters and hyphens.");
        }

        Value = normalized;
    }

    public static ProjectSlug Create(string value) => new(value);

    public static implicit operator string(ProjectSlug slug) => slug.Value;

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex GeneratedSlugRegex();
}
