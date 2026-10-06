using System.Text.RegularExpressions;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a validated, normalized email address.
/// </summary>
public sealed partial class EmailAddress : ValueObject
{
    public const int MaxLength = 256;
    private static readonly Regex EmailRegex = GeneratedEmailRegex();

    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static EmailAddress Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Email cannot be empty or whitespace.");
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            throw new DomainValidationException($"Email must not exceed {MaxLength} characters.");
        }

        if (!EmailRegex.IsMatch(normalized))
        {
            throw new DomainValidationException($"Email '{value}' has an invalid format.");
        }

        return new EmailAddress(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(EmailAddress email) => email.Value;

    [GeneratedRegex(@"^[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}$", RegexOptions.Compiled)]
    private static partial Regex GeneratedEmailRegex();
}
