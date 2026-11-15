using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Security;

namespace SmkDoc.Infrastructure.Notifications;

/// <summary>
/// Delivers signed outbound job lifecycle webhooks over an IP-pinned SSRF-hardened <see cref="HttpClient"/>.
/// Attaches <see cref="ProjectId"/> metadata so <see cref="SocketsHttpHandler.ConnectCallback"/> validates and pins IPs at connect time.
/// </summary>
public sealed partial class WebhookDispatcher(
    IHttpClientFactory httpClientFactory,
    IOptions<ApiKeyOptions> securityOptions,
    TimeProvider timeProvider,
    ILogger<WebhookDispatcher> logger) : IWebhookDispatcher
{
    public const string HttpClientName = "webhooks";
    public static readonly HttpRequestOptionsKey<ProjectId> ProjectIdOptionKey = new("SmkDoc.WebhookProjectId");

    public async Task DispatchAsync(WebhookDeliveryRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CallbackUrl.Scheme != Uri.UriSchemeHttp &&
            request.CallbackUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Unsupported webhook scheme '{request.CallbackUrl.Scheme}'.");
        }

        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Post, request.CallbackUrl);
        message.Options.Set(ProjectIdOptionKey, request.ProjectId);

        long timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        message.Headers.Add("X-SmkDoc-Event", request.EventType);
        message.Headers.Add("X-SmkDoc-Job-Id", request.JobId.ToString("D"));
        message.Headers.Add("X-SmkDoc-Delivery-Id", Guid.CreateVersion7().ToString("D"));

        string? secret = ResolveSigningSecret(securityOptions.Value);
        if (!string.IsNullOrWhiteSpace(secret))
        {
            string signature = ComputeSignature(secret, timestamp, request.PayloadJson);
            message.Headers.Add("Webhook-Signature", $"t={timestamp},v1={signature}");
        }

        message.Content = new StringContent(request.PayloadJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        if (logger.IsEnabled(LogLevel.Information))
        {
            LogWebhookDelivered(logger, request.JobId, request.EventType, (int)response.StatusCode);
        }
    }

    internal static string ComputeSignature(string secret, long unixTimestamp, string payloadJson)
    {
        string signedContent = string.Create(CultureInfo.InvariantCulture, $"{unixTimestamp}.{payloadJson}");
        byte[] hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedContent));
        return Convert.ToHexStringLower(hash);
    }

    private static string? ResolveSigningSecret(ApiKeyOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.WebhookSigningSecret))
        {
            return options.WebhookSigningSecret;
        }

        return string.IsNullOrWhiteSpace(options.ApiKeyPepper) ? null : options.ApiKeyPepper;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Webhook delivered for job {JobId} ({EventType}) with status {StatusCode}")]
    private static partial void LogWebhookDelivered(ILogger logger, Guid jobId, string eventType, int statusCode);
}
