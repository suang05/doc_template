using System.Text.Json;

namespace SmkDoc.Tests.Contract;

/// <summary>
/// Contract parity assertions against the build-generated specs/openapi.json document
/// (specs/00_architecture_core/06_api_contract_standards.md §2.2).
/// </summary>
public sealed class OpenApiContractParityTests
{
    [Fact]
    public void OpenApiSpec_MustDeclareRequiredSecuritySchemes()
    {
        using var document = LoadOpenApiDocument();
        var root = document.RootElement;

        var schemes = root.GetProperty("components").GetProperty("securitySchemes");
        schemes.TryGetProperty("ApiKey", out var apiKeyScheme).Should().BeTrue("ApiKey security scheme must exist");
        apiKeyScheme.GetProperty("name").GetString().Should().Be("X-API-Key");

        schemes.TryGetProperty("Bearer", out var bearerScheme).Should().BeTrue("Bearer JWT security scheme must exist");
        bearerScheme.GetProperty("scheme").GetString().Should().Be("bearer");
    }

    [Fact]
    public void OpenApiSpec_AllErrorResponses_MustUseProblemDetailsJsonSchema()
    {
        using var document = LoadOpenApiDocument();
        var paths = document.RootElement.GetProperty("paths");
        var violations = new List<string>();

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("responses", out var responses))
                {
                    continue;
                }

                foreach (var response in responses.EnumerateObject())
                {
                    if (!response.Name.StartsWith('4') && !response.Name.StartsWith('5'))
                    {
                        continue;
                    }

                    if (!response.Value.TryGetProperty("content", out var content) ||
                        !content.TryGetProperty("application/problem+json", out var problemContent))
                    {
                        violations.Add($"{operation.Name.ToUpperInvariant()} {path.Name} [{response.Name}]: missing application/problem+json");
                        continue;
                    }

                    var schemaRef = problemContent.GetProperty("schema").GetProperty("$ref").GetString();
                    if (!string.Equals(schemaRef, "#/components/schemas/ProblemDetails", StringComparison.Ordinal))
                    {
                        violations.Add($"{operation.Name.ToUpperInvariant()} {path.Name} [{response.Name}]: expected ProblemDetails schema, found {schemaRef}");
                    }
                }
            }
        }

        violations.Should().BeEmpty("all 4xx/5xx responses must conform to RFC 9457 ProblemDetails (ErrorEnvelopeFirewall)");
    }

    [Fact]
    public void OpenApiSpec_AllJsonSuccessResponses_MustWrapPayloadInDataEnvelope()
    {
        using var document = LoadOpenApiDocument();
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var violations = new List<string>();

        foreach (var path in paths.EnumerateObject())
        {
            if (path.Name.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("responses", out var responses))
                {
                    continue;
                }

                foreach (var response in responses.EnumerateObject())
                {
                    if (!response.Name.StartsWith('2'))
                    {
                        continue;
                    }

                    if (!response.Value.TryGetProperty("content", out var content) ||
                        !content.TryGetProperty("application/json", out var jsonContent))
                    {
                        continue;
                    }

                    if (!jsonContent.TryGetProperty("schema", out var schema) ||
                        !HasDataProperty(schema, schemas))
                    {
                        violations.Add($"{operation.Name.ToUpperInvariant()} {path.Name} [{response.Name}]: JSON response schema missing top-level 'data' envelope");
                    }
                }
            }
        }

        violations.Should().BeEmpty("all non-health 2xx JSON responses must wrap payloads in ApiResponse<T>/PagedApiResponse<T> (ResponseEnvelopeFirewall)");
    }

    private static bool HasDataProperty(JsonElement schema, JsonElement schemas)
    {
        if (schema.TryGetProperty("$ref", out var refProp))
        {
            var refName = refProp.GetString()?.Split('/').LastOrDefault();
            if (refName is not null && schemas.TryGetProperty(refName, out var targetSchema))
            {
                return HasDataProperty(targetSchema, schemas);
            }
        }

        return schema.TryGetProperty("properties", out var props) && props.TryGetProperty("data", out _);
    }

    private static JsonDocument LoadOpenApiDocument()
    {
        var copiedPath = Path.Combine(AppContext.BaseDirectory, "specs", "openapi.json");
        if (File.Exists(copiedPath))
        {
            return JsonDocument.Parse(File.ReadAllText(copiedPath));
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "specs", "openapi.json");
            if (File.Exists(candidate))
            {
                return JsonDocument.Parse(File.ReadAllText(candidate));
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate specs/openapi.json for contract parity verification.");
    }
}
