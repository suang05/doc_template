using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Backward compatibility alias for <see cref="ValidationErrorItem"/>.
/// </summary>
public record SchemaValidationError(string PropertyPath, string Message, string? SchemaRule = null)
    : ValidationErrorItem(PropertyPath, SchemaRule ?? string.Empty, Message);

/// <summary>
/// Thrown by <c>GenerateDocumentUseCase</c> when the incoming JSON payload fails Draft-07
/// schema validation. Maps to HTTP 400 Bad Request at the presentation layer.
/// </summary>
public sealed class SchemaValidationException : DomainException
{
    /// <summary>Slug of the template whose schema was violated.</summary>
    public string TemplateSlug { get; }

    /// <summary>Version number of the schema that was used for validation.</summary>
    public int Version { get; }

    /// <summary>All validation errors collected from the Draft-07 evaluation.</summary>
    public IReadOnlyList<ValidationErrorItem> Errors { get; }

    public SchemaValidationException(
        string templateSlug,
        int version,
        IReadOnlyList<ValidationErrorItem> errors,
        Exception? innerException = null)
        : base($"Payload validation failed for template '{templateSlug}' v{version} " +
               $"({errors.Count} error(s)).",
               "SCHEMA_VALIDATION_FAILED",
               400,
               innerException)
    {
        TemplateSlug = templateSlug;
        Version      = version;
        Errors       = errors;
    }
}
