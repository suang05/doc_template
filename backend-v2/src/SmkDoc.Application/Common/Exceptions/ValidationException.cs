using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown when incoming Command or Query parameters fail static input validation (e.g. via FluentValidation).
/// Maps to HTTP 400 Bad Request with an RFC 7807 problem details response containing an errors dictionary.
/// </summary>
public sealed class ValidationException : DomainException
{
    /// <summary>
    /// Dictionary of property names to error message arrays.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", "VALIDATION_FAILED")
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value?.ToArray() ?? []
        ).AsReadOnly();
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
}
