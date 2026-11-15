using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

public sealed record WebhookDeliveryRequest(
    ProjectId ProjectId,
    Guid JobId,
    Uri CallbackUrl,
    string EventType,
    string PayloadJson);

/// <summary>
/// Dispatches signed HMAC-SHA256 webhook callbacks over SSRF-hardened IP-pinned HTTP transport.
/// </summary>
public interface IWebhookDispatcher
{
    Task DispatchAsync(WebhookDeliveryRequest request, CancellationToken ct);
}
