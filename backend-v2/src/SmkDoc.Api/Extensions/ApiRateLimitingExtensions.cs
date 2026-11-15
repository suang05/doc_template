using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.RateLimiting;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.RateLimiting;
using StackExchange.Redis;

namespace SmkDoc.Api.Extensions;

internal static class ApiRateLimitingExtensions
{
    internal static void AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        bool useRedis = RedisConfigurationResolver.ResolveConnectionString(configuration) is not null;
        string instancePrefix = RedisConfigurationResolver.ResolveInstancePrefix(configuration);
        int globalLimit = Math.Max(1, configuration.GetValue("RateLimiting:Global:PermitLimit", 120));
        int authStrictLimit = Math.Max(1, configuration.GetValue("RateLimiting:AuthStrict:PermitLimit", 5));
        int apiLimit = Math.Max(1, configuration.GetValue("RateLimiting:Api:PermitLimit", 60));
        int renderingConcurrency = Math.Max(1, configuration.GetValue("RateLimiting:Rendering:ConcurrencyLimit", 20));
        int renderingQueue = Math.Max(0, configuration.GetValue("RateLimiting:Rendering:QueueLimit", 10));
        int connectivityCheckLimit = Math.Max(1, configuration.GetValue("RateLimiting:ConnectivityCheck:PermitLimit", 10));

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var path = httpContext.Request.Path;
                if (path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetNoLimiter("bypass");
                }

                var partitionKey = RateLimitPartitionKeyResolver.ResolveGlobalPartition(httpContext);
                return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = globalLimit,
                    TokensPerPeriod = globalLimit,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("auth-strict", context =>
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString();
                var partitionKey = string.IsNullOrWhiteSpace(remoteIp) ? "ip:unknown" : $"ip:{remoteIp}";
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey,
                    _ => CreateSlidingWindowOptions(authStrictLimit));
            });

            options.AddPolicy("api", context => CreateFixedWindowPartition(context, apiLimit, useRedis, instancePrefix));

            options.AddPolicy("rendering", context =>
            {
                var partitionKey = RateLimitPartitionKeyResolver.Resolve(context);
                return RateLimitPartition.GetConcurrencyLimiter(partitionKey, _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = renderingConcurrency,
                    QueueLimit = renderingQueue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });

            options.AddPolicy("connectivity-check", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    RateLimitPartitionKeyResolver.Resolve(context),
                    _ => CreateSlidingWindowOptions(connectivityCheckLimit)));

            options.AddPolicy("test-conn", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    RateLimitPartitionKeyResolver.Resolve(context),
                    _ => CreateSlidingWindowOptions(connectivityCheckLimit)));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, ct) =>
            {
                string? policyName = context.HttpContext.GetEndpoint()?.Metadata
                    .GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                return RateLimitRejectionWriter.WriteAsync(context, configuration, policyName, ct);
            };
        });
    }

    private static SlidingWindowRateLimiterOptions CreateSlidingWindowOptions(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        SegmentsPerWindow = 4,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
    };

    private static FixedWindowRateLimiterOptions CreateFixedWindowOptions(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
    };

    private static RateLimitPartition<string> CreateFixedWindowPartition(
        HttpContext context,
        int permitLimit,
        bool useRedis,
        string instancePrefix)
    {
        var partitionKey = RateLimitPartitionKeyResolver.Resolve(context);
        var windowOptions = CreateFixedWindowOptions(permitLimit);

        if (useRedis)
        {
            var multiplexer = context.RequestServices.GetService<IConnectionMultiplexer>();
            if (multiplexer is not null)
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger<RedisFixedWindowRateLimiter>();

                return RateLimitPartition.Get(
                    partitionKey,
                    key => new RedisFixedWindowRateLimiter(multiplexer, key, windowOptions, logger, instancePrefix));
            }
        }

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => windowOptions);
    }
}
