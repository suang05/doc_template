using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace SmkDoc.Api.HealthChecks;

public sealed class RedisHealthCheck(IConnectionMultiplexer multiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var latency = await multiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis PING {latency.TotalMilliseconds:F2} ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}
