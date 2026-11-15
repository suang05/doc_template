namespace SmkDoc.Api.Constants;

/// <summary>
/// Technical HTTP Header name constants for authentication and tenant context.
/// </summary>
public static class HttpHeaderConstants
{
    /// <summary>
    /// HTTP Header for workspace context override.
    /// </summary>
    public const string ProjectIdHeader = "Project-Id";

    /// <summary>
    /// HTTP Header carrying bearer or M2M API key credentials.
    /// </summary>
    public const string AuthorizationHeader = "Authorization";

    /// <summary>
    /// HTTP Authorization scheme for Machine-to-Machine API keys (<c>Authorization: ApiKey &lt;rawKey&gt;</c>).
    /// </summary>
    public const string AuthorizationApiKeyScheme = "ApiKey";

    /// <summary>
    /// Header prefix for M2M API key credentials (<c>ApiKey </c>).
    /// </summary>
    public const string AuthorizationApiKeyPrefix = "ApiKey ";

    /// <summary>
    /// Extracts the raw M2M credential from <c>Authorization: ApiKey &lt;rawKey&gt;</c>.
    /// </summary>
    public static bool TryExtractApiKey(IHeaderDictionary headers, out string apiKey)
    {
        if (headers.TryGetValue(AuthorizationHeader, out var values))
        {
            foreach (var rawHeader in values)
            {
                if (string.IsNullOrWhiteSpace(rawHeader))
                {
                    continue;
                }

                var trimmed = rawHeader.Trim();
                if (trimmed.StartsWith(AuthorizationApiKeyScheme, StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Length > AuthorizationApiKeyScheme.Length &&
                    char.IsWhiteSpace(trimmed[AuthorizationApiKeyScheme.Length]))
                {
                    var candidate = trimmed[(AuthorizationApiKeyScheme.Length + 1)..].Trim();
                    if (!string.IsNullOrEmpty(candidate))
                    {
                        apiKey = candidate;
                        return true;
                    }
                }
            }
        }

        apiKey = string.Empty;
        return false;
    }
}
