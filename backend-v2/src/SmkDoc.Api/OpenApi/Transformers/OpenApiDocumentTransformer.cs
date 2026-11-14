using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SmkDoc.Api.OpenApi.Transformers;

/// <summary>
/// Document-level OpenAPI metadata: info, servers, security schemes (ApiKey OR Bearer) and the RFC 9457 error schema.
/// </summary>
public sealed class OpenApiDocumentTransformer(IConfiguration configuration) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Step 1: Info & servers
        document.Info = BuildInfo();
        document.Servers = [BuildServer()];

        // Step 2: Components (security schemes + error schema)
        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes[OpenApiNames.ApiKeyScheme] = BuildApiKeyScheme();
        components.SecuritySchemes[OpenApiNames.BearerScheme] = BuildBearerScheme();
        components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        components.Schemas[OpenApiNames.ProblemDetailsSchema] = BuildProblemDetailsSchema();

        // Step 3: Global security — two separate requirements means "ApiKey OR Bearer"
        document.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(OpenApiNames.ApiKeyScheme, document)] = [] },
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(OpenApiNames.BearerScheme, document)] = [] }
        ];

        // Step 4: Operation ids must be unique across aliased routes
        OpenApiOperationIdDeduplicator.Apply(document);

        return Task.CompletedTask;
    }

    private static OpenApiInfo BuildInfo() => new()
    {
        Title = "SAMMAKORN Document Generation Service API",
        Version = OpenApiNames.ApiVersion,
        Description =
            "Enterprise document generation microservice accepting JSON payloads and generating pixel-perfect PDF, DOCX, or XLSX output based on SDD v1.3."
    };

    private OpenApiServer BuildServer()
    {
        var configuredUrl = configuration[OpenApiNames.ServerUrlConfigKey];
        var url = string.IsNullOrWhiteSpace(configuredUrl) ? OpenApiNames.DefaultServerUrl : configuredUrl.Trim();
        return new OpenApiServer { Url = url, Description = "Document Server API host" };
    }

    private static OpenApiSecurityScheme BuildApiKeyScheme() => new()
    {
        Description = $"Machine-to-machine API key. Send it in the {OpenApiNames.ApiKeyHeader} header.",
        Name = OpenApiNames.ApiKeyHeader,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    };

    private static OpenApiSecurityScheme BuildBearerScheme() => new()
    {
        Description = "Portal user JWT. Send it as: Authorization: Bearer <token>.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    private static OpenApiSchema BuildProblemDetailsSchema() => new()
    {
        Type = JsonSchemaType.Object,
        Description = "RFC 9457 problem details returned for every 4xx/5xx response.",
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "URI identifying the problem type." },
            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Short human-readable summary." },
            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32", Description = "HTTP status code." },
            ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Human-readable explanation with remediation guidance." },
            ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Request path that produced the problem." },
            ["errorCode"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Stable machine-readable error code." },
            ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Correlation trace identifier for observability and log lookup." },
            ["errors"] = BuildFieldErrorsSchema()
        }
    };

    private static OpenApiSchema BuildFieldErrorsSchema() => new()
    {
        Description = "RFC 9457 field-level validation and JSON schema errors with RFC 6901 JSON Pointers.",
        Type = JsonSchemaType.Array,
        Items = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "pointer", "code", "message" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["pointer"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "RFC 6901 JSON Pointer targeting the offending property or document location." },
                ["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Machine-readable validation rule code in UPPER_SNAKE_CASE." },
                ["message"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Human-readable validation failure description." }
            }
        }
    };
}

