using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Infrastructure.Schema;

/// <summary>
/// Infrastructure adapter providing high-performance, bounded-memory JSON Schema Draft-07 validation
/// powered by <c>JsonSchema.Net</c>.
/// </summary>
/// <remarks>
/// Registered as Singleton. Implements the Port <see cref="IJsonSchemaValidationService"/>
/// and maps external <c>JsonSchema.Net</c> evaluation results directly into Domain
/// <see cref="SchemaValidationResult"/> and <see cref="ValidationErrorItem"/>.
/// </remarks>
public sealed class JsonSchemaValidationService : IJsonSchemaValidationService
{
    private static readonly EvaluationOptions Draft7Options = new()
    {
        OutputFormat = OutputFormat.List,
        EvaluateAs = SpecVersion.Draft7,
        RequireFormatValidation = true
    };

    private static readonly MemoryCacheEntryOptions CacheOptions = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromHours(2))
        .SetAbsoluteExpiration(TimeSpan.FromHours(8))
        .SetSize(1);

    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public JsonSchemaValidationService(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public SchemaValidationResult Validate(string schemaJson, string dataJson)
    {
        var (schema, schemaError) = GetOrCompileSchema(schemaJson);
        if (schemaError != null)
        {
            return SchemaValidationResult.Failure(schemaError);
        }

        var (dataNode, parseError) = ParsePayload(dataJson);
        if (parseError != null)
        {
            return SchemaValidationResult.Failure(parseError);
        }

        return EvaluateInternal(schema!, dataNode!);
    }

    /// <inheritdoc />
    public SchemaValidationResult Validate(string schemaJson, JsonElement dataElement)
    {
        var (schema, schemaError) = GetOrCompileSchema(schemaJson);
        if (schemaError != null)
        {
            return SchemaValidationResult.Failure(schemaError);
        }

        var (dataNode, parseError) = ParsePayload(dataElement);
        if (parseError != null)
        {
            return SchemaValidationResult.Failure(parseError);
        }

        return EvaluateInternal(schema!, dataNode!);
    }

    /// <inheritdoc />
    public SchemaValidationResult Validate(JsonElement schemaElement, JsonElement dataElement)
    {
        if (schemaElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return SchemaValidationResult.Failure(
                new ValidationErrorItem("/", "empty-schema", "Schema definition is missing or null."));
        }

        string schemaJson = schemaElement.GetRawText();
        return Validate(schemaJson, dataElement);
    }

    private SchemaValidationResult EvaluateInternal(JsonSchema schema, JsonNode node)
    {
        var evaluation = schema.Evaluate(node, Draft7Options);
        if (evaluation.IsValid)
        {
            return SchemaValidationResult.Success();
        }

        var errors = ExtractValidationErrors(evaluation);
        return SchemaValidationResult.Failure(errors);
    }

    private (JsonSchema? Schema, ValidationErrorItem? Error) GetOrCompileSchema(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return (null, new ValidationErrorItem("/", "empty-schema", "Schema definition is empty."));
        }

        string cacheKey = ComputeCacheKey(schemaJson);

        if (_cache.TryGetValue(cacheKey, out JsonSchema? cachedSchema) && cachedSchema != null)
        {
            return (cachedSchema, null);
        }

        object keyLock = _locks.GetOrAdd(cacheKey, _ => new object());
        lock (keyLock)
        {
            if (_cache.TryGetValue(cacheKey, out cachedSchema) && cachedSchema != null)
            {
                return (cachedSchema, null);
            }

            try
            {
                var schema = JsonSchema.FromText(schemaJson);
                _cache.Set(cacheKey, schema, CacheOptions);
                return (schema, null);
            }
            catch (Exception ex)
            {
                var error = new ValidationErrorItem(
                    "/",
                    "schema-parse",
                    $"Stored schema is not valid JSON Schema: {ex.Message}");
                return (null, error);
            }
            finally
            {
                _locks.TryRemove(cacheKey, out _);
            }
        }
    }

    private static (JsonNode? Node, ValidationErrorItem? Error) ParsePayload(string dataJson)
    {
        try
        {
            var node = JsonNode.Parse(dataJson);
            return (node, null);
        }
        catch (Exception ex)
        {
            var error = new ValidationErrorItem(
                "/",
                "json-parse",
                $"Payload is not valid JSON: {ex.Message}");
            return (null, error);
        }
    }

    private static (JsonNode? Node, ValidationErrorItem? Error) ParsePayload(JsonElement dataElement)
    {
        try
        {
            var node = JsonNode.Parse(dataElement.GetRawText());
            return (node, null);
        }
        catch (Exception ex)
        {
            var error = new ValidationErrorItem(
                "/",
                "json-parse",
                $"Payload is not valid JSON: {ex.Message}");
            return (null, error);
        }
    }

    private static List<ValidationErrorItem> ExtractValidationErrors(EvaluationResults results)
    {
        var errors = results.Details
            .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e =>
            {
                string path = d.InstanceLocation.ToString();
                if (string.IsNullOrEmpty(path)) path = "/";
                return new ValidationErrorItem(path, e.Key, e.Value);
            }))
            .ToList();

        if (errors.Count == 0)
        {
            errors.Add(new ValidationErrorItem("/", "schema-validation", "Payload does not conform to the template schema."));
        }

        return errors;
    }

    private static string ComputeCacheKey(string input)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"schema_v7_{Convert.ToHexString(hash)}";
    }
}
