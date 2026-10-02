using System.Text.Json;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Common.Interfaces;

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
    /// <returns>A <see cref="SchemaValidationResult"/> from Domain layer.</returns>
    SchemaValidationResult Validate(string schemaJson, string dataJson);

    /// <summary>
    /// Validates <paramref name="dataElement"/> against the Draft-07 schema contained in
    /// <paramref name="schemaJson"/>.
    /// </summary>
    SchemaValidationResult Validate(string schemaJson, JsonElement dataElement);

    /// <summary>
    /// Validates <paramref name="dataElement"/> against the Draft-07 schema contained in
    /// <paramref name="schemaElement"/>.
    /// </summary>
    SchemaValidationResult Validate(JsonElement schemaElement, JsonElement dataElement);
}
