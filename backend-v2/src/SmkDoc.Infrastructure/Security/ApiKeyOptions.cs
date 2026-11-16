namespace SmkDoc.Infrastructure.Security;

/// <summary>
/// Configuration binding for HMAC-SHA256 API key minting/verification and legacy SHA-256 transition support.
/// </summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "Security";

    public string ApiKeyPepper { get; set; } = string.Empty;
    public bool AllowLegacySha256Keys { get; set; }
    public string WebhookSigningSecret { get; set; } = string.Empty;
}
