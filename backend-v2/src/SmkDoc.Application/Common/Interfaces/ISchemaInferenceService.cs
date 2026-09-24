namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Domain-level abstraction for inferring JSON Schema (Draft-07) and generating default mock payloads
/// from template placeholder tokens across HTML, Word (DOCX), and Excel (XLSX).
/// </summary>
public interface ISchemaInferenceService
{
    /// <summary>
    /// Infers a JSON Schema (Draft-07) from a list of placeholder tokens (e.g. "customer_name", "grand_total:number", "items.price").
    /// If samplePayloadJson is provided, merges and validates types against the sample payload.
    /// Supports distinguishing between single nested objects and array collections.
    /// </summary>
    string InferSchemaFromPlaceholders(IEnumerable<string> placeholders, string? samplePayloadJson = null, string? templateSlug = null);

    /// <summary>
    /// Generates a realistic mock JSON payload conforming to detected types from placeholders.
    /// </summary>
    string GenerateDefaultSamplePayload(IEnumerable<string> placeholders);

    /// <summary>
    /// Infers a JSON Schema (Draft-07) and default sample payload directly from an HTML / Handlebars template string.
    /// Accurately detects {{#each items}} as array collections and dot-notation outside loops as single nested objects.
    /// </summary>
    (string DataSchema, string SamplePayload) InferFromHtml(string htmlContent, string? customSamplePayloadJson = null, string? templateSlug = null);
}
