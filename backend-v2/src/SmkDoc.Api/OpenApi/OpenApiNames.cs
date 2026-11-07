using SmkDoc.Api.Constants;
using SmkDoc.Api.Filters;

namespace SmkDoc.Api.OpenApi;

/// <summary>
/// Single source of truth for names and constants shared by the OpenAPI transformers and perimeter middleware.
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
    public const string ApiKeyHeader = HttpHeaderConstants.ApiKeyHeader;
    public const string IdempotencyKeyHeader = IdempotencyFilter.HeaderName;
    public const string IfMatchHeader = "If-Match";
    public const string ETagHeader = "ETag";

    /// <summary>
    /// API v1 routes that require no authentication.
    /// Consumed by both <c>ProjectContextMiddleware</c> and <c>OpenApiOperationTransformer</c>.
    /// </summary>
    public static readonly string[] PublicRoutes =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/refresh",
        "/api/v1/schemas/validate"
    ];
}

