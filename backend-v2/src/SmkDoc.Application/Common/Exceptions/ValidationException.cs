namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown when incoming Command or Query parameters fail static input validation (e.g. via FluentValidation).
/// Maps to HTTP 422 Unprocessable Entity with an RFC 9457 problem details response containing RFC 6901 pointer entries.
/// </summary>
public sealed class ValidationException : DomainException
{
    /// <summary>
    /// Dictionary of property names to error message arrays.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Normalized RFC 9457 validation error entries with RFC 6901 JSON pointers and machine codes.
    /// </summary>
    public IReadOnlyList<ValidationErrorEntry> ErrorEntries { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", ErrorCodes.ValidationFailed)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = CopyErrors(errors);
        ErrorEntries = Errors
            .SelectMany(kvp => kvp.Value.Select(message => new ValidationErrorEntry(
                ValidationErrorEntry.ToJsonPointer(kvp.Key, camelCaseSegments: true),
                "VALIDATION_ERROR",
                message)))
            .ToArray()
            .AsReadOnly();
    }

    public ValidationException(IDictionary<string, string[]> errors, IEnumerable<ValidationErrorEntry> errorEntries)
        : base("One or more validation errors occurred.", ErrorCodes.ValidationFailed)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(errorEntries);
        Errors = CopyErrors(errors);
        ErrorEntries = errorEntries.ToArray().AsReadOnly();
    }

    public ValidationException(IEnumerable<ValidationErrorEntry> errorEntries)
        : base("One or more validation errors occurred.", ErrorCodes.ValidationFailed)
    {
        ArgumentNullException.ThrowIfNull(errorEntries);
        var entries = errorEntries.ToArray();
        ErrorEntries = entries.AsReadOnly();
        Errors = entries
            .GroupBy(entry => NormalizeDictionaryKey(entry.Pointer))
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Message).Distinct().ToArray())
            .AsReadOnly();
    }

    public ValidationException(string propertyName, string errorMessage)
        : this(new Dictionary<string, string[]>
        {
            [propertyName] = [errorMessage]
        })
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
    }

    private static IDictionary<string, string[]> CopyErrors(IDictionary<string, string[]> errors) =>
        errors.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value?.ToArray() ?? []
        ).AsReadOnly();

    private static string NormalizeDictionaryKey(string pointer)
    {
        var trimmed = pointer.TrimStart('/');
        return string.IsNullOrEmpty(trimmed) ? "request" : trimmed.Replace('/', '.');
    }
}
