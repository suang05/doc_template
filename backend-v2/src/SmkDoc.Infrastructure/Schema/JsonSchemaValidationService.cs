using Json.Schema;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Infrastructure.Schema;

/// <summary>
/// Implements <see cref="IJsonSchemaValidationService"/> using the
/// <c>JsonSchema.Net</c> library, evaluating payloads against JSON Schema Draft-07.
/// </summary>
/// <remarks>
/// Registered as Singleton — <c>JsonSchema</c> instances are thread-safe once parsed.
/// </remarks>
public sealed class JsonSchemaValidationService : IJsonSchemaValidationService
{
    /// <inheritdoc />
    public SchemaValidationResult Validate(string schemaJson, string dataJson)
    {
        // Parse the Draft-07 schema string.
        JsonSchema schema;
        try
        {
            schema = JsonSchema.FromText(schemaJson);
        }
        catch (Exception ex)
        {
            // Malformed schema stored in DB — treat as a single structural error so the
            // caller knows the schema itself is broken, not the payload.
            var schemaError = new SchemaValidationError(
                "/",
                $"Stored schema is not valid JSON Schema: {ex.Message}",
                "schema-parse");
            return new SchemaValidationResult(false, [schemaError]);
        }

        // Parse the incoming payload.
        System.Text.Json.Nodes.JsonNode? dataNode;
        try
        {
            dataNode = System.Text.Json.Nodes.JsonNode.Parse(dataJson);
        }
        catch (Exception ex)
        {
            var parseError = new SchemaValidationError(
                "/",
                $"Payload is not valid JSON: {ex.Message}",
                "json-parse");
            return new SchemaValidationResult(false, [parseError]);
        }

        // Evaluate with output format that gives per-annotation detail.
        var options = new EvaluationOptions
        {
            OutputFormat        = OutputFormat.List,
            EvaluateAs          = SpecVersion.Draft7,
            RequireFormatValidation = true
        };

        EvaluationResults results = schema.Evaluate(dataNode, options);

        if (results.IsValid)
            return new SchemaValidationResult(true, []);

        // Collect failures from the flat List output — only leaf failures with an error message.
        var errors = results.Details
            .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e =>
            {
                string path = d.InstanceLocation.ToString();
                // Normalize to "/" root if empty
                if (string.IsNullOrEmpty(path)) path = "/";
                return new SchemaValidationError(path, e.Value, d.EvaluationPath.ToString());
            }))
            .ToList();

        // Fallback: if no granular errors were extracted, emit a generic one.
        if (errors.Count == 0)
        {
            errors.Add(new SchemaValidationError("/", "Payload does not conform to the template schema.", null));
        }

        return new SchemaValidationResult(false, errors);
    }
}
