using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

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

    public static ProjectId NotEmpty(ProjectId value, string name) =>
        value.IsEmpty
            ? throw new DomainValidationException($"{name} cannot be empty.")
            : value;

    public static CompanyId NotEmpty(CompanyId value, string name) =>
        value.IsEmpty
            ? throw new DomainValidationException($"{name} cannot be empty.")
            : value;

    public static TemplateId NotEmpty(TemplateId value, string name) =>
        value.IsEmpty
            ? throw new DomainValidationException($"{name} cannot be empty.")
            : value;

    public static DataConnectionId NotEmpty(DataConnectionId value, string name) =>
        value.IsEmpty
            ? throw new DomainValidationException($"{name} cannot be empty.")
            : value;

    public static UserId NotEmpty(UserId value, string name) =>
        value.IsEmpty
            ? throw new DomainValidationException($"{name} cannot be empty.")
            : value;

    public static DocumentId NotEmpty(DocumentId value, string name) =>
        value.IsEmpty
            ? throw new DomainValidationException($"{name} cannot be empty.")
            : value;

    public static ApiKeyId NotEmpty(ApiKeyId value, string name) =>
        value.IsEmpty
            ? throw new DomainValidationException($"{name} cannot be empty.")
            : value;

    public static DatasetId NotEmpty(DatasetId value, string name) =>
        value.IsEmpty
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
