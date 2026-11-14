using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace SmkDoc.Api.HealthChecks;

public sealed class RedisHealthCheck(IConnectionMultiplexer multiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var latency = await multiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis PING {latency.TotalMilliseconds:F2} ms");
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}
