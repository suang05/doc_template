namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Represents an individual validation error for a specific field/path in the payload.
/// </summary>
/// <param name="PropertyPath">JSON Pointer path (e.g. "/customer/tax_id").</param>
/// <param name="Message">Human-readable error description.</param>
/// <param name="SchemaRule">The Draft-07 keyword that triggered the error (e.g. "pattern", "required").</param>
public record SchemaValidationError(
    string PropertyPath,
    string Message,
    string? SchemaRule = null);

/// <summary>
/// Thrown by <c>GenerateDocumentUseCase</c> when the incoming JSON payload fails Draft-07
/// schema validation. Maps to HTTP 400 Bad Request at the presentation layer.
/// </summary>
public sealed class SchemaValidationException : Exception
{
    /// <summary>Slug of the template whose schema was violated.</summary>
    public string TemplateSlug { get; }

    /// <summary>Version number of the schema that was used for validation.</summary>
    public int Version { get; }

    /// <summary>All validation errors collected from the Draft-07 evaluation.</summary>
    public IReadOnlyList<SchemaValidationError> Errors { get; }

    public SchemaValidationException(
        string templateSlug,
        int version,
        IReadOnlyList<SchemaValidationError> errors)
        : base($"Payload validation failed for template '{templateSlug}' v{version} " +
               $"({errors.Count} error(s)).")
    {
        TemplateSlug = templateSlug;
        Version      = version;
        Errors       = errors;
    }
}
