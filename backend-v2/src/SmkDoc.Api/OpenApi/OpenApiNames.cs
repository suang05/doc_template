namespace SmkDoc.Api.OpenApi;

/// <summary>
/// Single source of truth for names and constants shared by the OpenAPI transformers.
/// </summary>
public static class OpenApiNames
{
    public const string ApiKeyScheme = "ApiKey";
    public const string BearerScheme = "Bearer";

    public const string ProblemDetailsSchema = "ProblemDetails";
    public const string ProblemJsonMediaType = "application/problem+json";

    public const string ServerUrlConfigKey = "OpenApi:ServerUrl";
    public const string DefaultServerUrl = "/";

    public const string ApiVersion = "2.0.0";
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Routes that require no authentication.
    /// Mirror of the whitelist in <c>ProjectContextMiddleware</c> restricted to the /api/v1 surface.
    /// </summary>
    public static readonly string[] PublicRoutes =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/refresh",
        "/api/v1/schemas/validate"
    ];
}

