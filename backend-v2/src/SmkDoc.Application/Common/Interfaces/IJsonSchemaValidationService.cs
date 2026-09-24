using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Represents the result of a Draft-07 JSON Schema validation run.
/// </summary>
/// <param name="IsValid">True when the data fully satisfies the schema.</param>
/// <param name="Errors">Collection of per-field violation details (empty when valid).</param>
public record SchemaValidationResult(
    bool IsValid,
    IReadOnlyList<SchemaValidationError> Errors);

/// <summary>
/// Port for runtime JSON Schema Draft-07 validation.
/// Implemented in the Infrastructure layer by <c>JsonSchemaValidationService</c>.
/// </summary>
public interface IJsonSchemaValidationService
{
    /// <summary>
    /// Validates <paramref name="dataJson"/> against the Draft-07 schema contained in
    /// <paramref name="schemaJson"/>.
    /// </summary>
    /// <param name="schemaJson">A JSON Schema Draft-07 string (from <c>template_versions.data_schema</c>).</param>
    /// <param name="dataJson">The raw JSON payload string to validate.</param>
    /// <returns>A <see cref="SchemaValidationResult"/> describing whether validation passed and listing any errors.</returns>
    SchemaValidationResult Validate(string schemaJson, string dataJson);
}
