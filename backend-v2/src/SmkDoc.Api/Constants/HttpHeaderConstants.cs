namespace SmkDoc.Api.Constants;

/// <summary>
/// Technical HTTP Header name constants for authentication and tenant context.
/// </summary>
public static class HttpHeaderConstants
{
    /// <summary>
    /// HTTP Header for workspace context override.
    /// </summary>
    public const string ProjectIdHeader = "X-Project-Id";

    /// <summary>
    /// HTTP Header for API Key authentication.
    /// </summary>
    public const string ApiKeyHeader = "X-API-Key";
}
