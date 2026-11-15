namespace SmkDoc.Infrastructure.Notifications;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhooks";

    public int TimeoutSeconds { get; set; } = 15;
}
