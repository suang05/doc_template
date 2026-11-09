using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Infrastructure.BackgroundWorkers;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Queue;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Extensions;

public static class DistributedInfrastructureExtensions
{
    public static IServiceCollection AddDistributedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DocumentRenderWorkerOptions>(configuration.GetSection("DocumentRenderWorker"));
        services.Configure<DocumentRetentionWorkerOptions>(configuration.GetSection("DocumentRetentionWorker"));

        var redisConn = RedisConfigurationResolver.ResolveConnectionString(configuration);
        var prefix = RedisConfigurationResolver.ResolveInstancePrefix(configuration);

        // 1. Unified Hybrid Draft Cache (L1 Memory + Optional L2 Redis)
        services.AddSingleton<ITemplateDraftCache>(sp => new HybridTemplateDraftCache(
            sp.GetService<IConnectionMultiplexer>(),
            sp.GetRequiredService<IStorageService>(),
            sp.GetRequiredService<ILogger<HybridTemplateDraftCache>>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            prefix));

        // 2. Distributed Store & Streams Queue
        if (redisConn is not null)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

            services.AddSingleton<IDatasetResultCache>(sp => new RedisDatasetResultCache(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<ILogger<RedisDatasetResultCache>>(),
                prefix));

            services.AddSingleton<IIdempotencyStore>(sp => new RedisIdempotencyStore(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<RedisIdempotencyStore>>(),
                prefix));

            services.AddSingleton<IDocumentRenderQueue>(sp =>
            {
                var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
                var logger = sp.GetRequiredService<ILogger<RedisDocumentRenderQueue>>();
                return new RedisDocumentRenderQueue(multiplexer, logger, prefix);
            });
        }
        else
        {
            services.AddSingleton<IDatasetResultCache, MemoryDatasetResultCache>();
            services.AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>();
            services.AddSingleton<IDocumentRenderQueue, MemoryDocumentRenderQueue>();
        }

        // 3. Background Hosted Workers
        services.AddHostedService<DocumentRenderQueueWorker>();
        services.AddHostedService<DocumentRetentionWorker>();

        return services;
    }
}

