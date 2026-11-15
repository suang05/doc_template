using System.Text.Json;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.EventHandlers;

internal static class DocumentJobWebhookPayloadBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static Task DispatchIfConfiguredAsync<TPayload>(
        IWebhookDispatcher webhookDispatcher,
        ProjectId projectId,
        DocumentJobId jobId,
        Uri? webhookUrl,
        string eventType,
        TPayload payload,
        CancellationToken ct)
    {
        if (webhookUrl is null)
        {
            return Task.CompletedTask;
        }

        string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        return webhookDispatcher.DispatchAsync(
            new WebhookDeliveryRequest(projectId, jobId.Value, webhookUrl, eventType, payloadJson),
            ct);
    }
}
