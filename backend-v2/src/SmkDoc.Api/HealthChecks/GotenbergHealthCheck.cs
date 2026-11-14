using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmkDoc.Api.HealthChecks;

public sealed class GotenbergHealthCheck(IHttpClientFactory factory) : IHealthCheck
{
    private static readonly Uri HealthEndpoint = new("/health", UriKind.Relative);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = factory.CreateClient("gotenberg-health");
            using var response = await client.GetAsync(HealthEndpoint, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
