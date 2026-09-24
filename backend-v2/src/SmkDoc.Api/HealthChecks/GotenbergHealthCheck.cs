using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmkDoc.Api.HealthChecks;

public class GotenbergHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _factory;

    public GotenbergHealthCheck(IHttpClientFactory factory) => _factory = factory;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var client = _factory.CreateClient("gotenberg-health");
            var response = await client.GetAsync("/health", ct);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
