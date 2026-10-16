namespace SmkDoc.Domain.Constants;

/// <summary>
/// Technical security constants for token lifetimes, auth schemes, and session policies.
/// </summary>
public static class SecurityConstants
{
    /// <summary>
    /// Default Access Token lifetime in seconds (24 hours).
    /// </summary>
    public const int AccessTokenLifetimeSeconds = 86400;

    /// <summary>
    /// Default Refresh Token lifetime in days (7 days).
    /// </summary>
    public const int RefreshTokenLifetimeDays = 7;

    /// <summary>
    /// Standard Bearer auth scheme name.
    /// </summary>
    public const string BearerTokenType = "Bearer";

    /// <summary>
    /// HTTP Header for workspace context override.
    /// </summary>
    public const string ProjectIdHeader = "X-Project-Id";

    /// <summary>
    /// HTTP Header for API Key authentication.
    /// </summary>
    public const string ApiKeyHeader = "X-API-Key";

    /// <summary>
    /// Concurrency grace period (in seconds) during Refresh Token Rotation to tolerate multi-tab parallel requests.
    /// </summary>
    public const int RefreshTokenReuseLeewaySeconds = 30;

    /// <summary>
    /// Pre-computed standard bcrypt hash used for constant-time password verification on user-not-found to mitigate timing enumeration attacks (OWASP).
    /// </summary>
    public const string DummyPasswordHash = "$2a$11$Vf5cHJJt/FIM7svpRfhP1en9WVEbvEIwB27OOnI.5EAWMEjQE/z6S";
}
