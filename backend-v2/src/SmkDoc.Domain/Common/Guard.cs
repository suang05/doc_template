using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Common;

/// <summary>
/// Fail-fast invariant guards. Always throws <see cref="DomainValidationException"/> (never ArgumentException),
/// so the presentation layer can map every violation to RFC 7807 consistently.
/// </summary>
internal static class Guard
{
    public static Guid NotEmpty(Guid value, string name) =>
        value == Guid.Empty
            ? throw new DomainValidationException($"{name} cannot be empty.")
            : value;

    public static int Positive(int value, string name) =>
        value <= 0
            ? throw new DomainValidationException($"{name} must be greater than zero.")
            : value;

    public static string NotBlank(string? value, string name, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{name} cannot be empty or whitespace.");
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength
            ? throw new DomainValidationException($"{name} must not exceed {maxLength} characters.")
            : trimmed;
    }

    public static string MaxLength(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length > maxLength
            ? throw new DomainValidationException($"{name} must not exceed {maxLength} characters.")
            : trimmed;
    }

    public static T NotNull<T>(T? value, string name) where T : class =>
        value ?? throw new DomainValidationException($"{name} is required.");
}
