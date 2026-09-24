using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SmkDoc.Infrastructure.Storage;

namespace SmkDoc.Api.HealthChecks;

public class MinioHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _factory;
    private readonly MinioSettings _settings;

    public MinioHealthCheck(IHttpClientFactory factory, IOptions<MinioSettings> settings)
    {
        _factory = factory;
        _settings = settings.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            string scheme = _settings.Secure ? "https" : "http";
            using var client = _factory.CreateClient("minio-health");
            var response = await client.GetAsync($"{scheme}://{_settings.Endpoint}/minio/health/live", ct);
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
