using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Notifications;
using SmkDoc.Infrastructure.Persistence.Idempotency;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Extensions;

public static class DistributedInfrastructureExtensions
{
    public static IServiceCollection AddDistributedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WebhookOptions>(configuration.GetSection(WebhookOptions.SectionName));

        var redisConn = RedisConfigurationResolver.ResolveConnectionString(configuration);
        var prefix = RedisConfigurationResolver.ResolveInstancePrefix(configuration);

        // 1. Unified Hybrid Draft Cache (L1 Memory + Optional L2 Redis)
        services.AddScoped<ITemplateDraftCache>(sp => new HybridTemplateDraftCache(
            sp.GetService<IConnectionMultiplexer>(),
            sp.GetRequiredService<IStorageService>(),
            sp.GetRequiredService<ILogger<HybridTemplateDraftCache>>(),
            sp.GetRequiredService<IMemoryCache>(),
            prefix,
            sp.GetService<ICurrentProjectContext>()));

        // 2. Dataset Cache (Optional Redis L2; falls back gracefully to in-memory cache)
        if (redisConn is not null)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = ConfigurationOptions.Parse(redisConn);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 5;
                return ConnectionMultiplexer.Connect(options);
            });

            services.AddSingleton<IDatasetResultCache>(sp => new RedisDatasetResultCache(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<ILogger<RedisDatasetResultCache>>(),
                prefix));
        }
        else
        {
            services.AddSingleton<IDatasetResultCache, MemoryDatasetResultCache>();
        }

        // 3. Durable PostgreSQL Idempotency Store (atomic UPSERT + RLS)
        services.AddSingleton<IIdempotencyStore>(sp => new PostgresIdempotencyStore(
            sp.GetRequiredService<NpgsqlDataSource>(),
            sp.GetRequiredService<TimeProvider>()));

        // 4. Outbound IP-Pinned Webhook HTTP Client (SSRF-hardened ConnectCallback + AllowAutoRedirect=false)
        services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
        services.AddHttpClient(WebhookDispatcher.HttpClientName, (sp, client) =>
        {
            int timeoutSeconds = Math.Clamp(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WebhookOptions>>().Value.TimeoutSeconds,
                1,
                120);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        })
        .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var host = context.DnsEndPoint.Host;
                var port = context.DnsEndPoint.Port;

                ProjectId? projectId = null;
                if (context.InitialRequestMessage?.Options.TryGetValue(WebhookDispatcher.ProjectIdOptionKey, out var pid) == true &&
                    !pid.IsEmpty)
                {
                    projectId = pid;
                }

                await using var scope = sp.CreateAsyncScope();
                var hostValidator = scope.ServiceProvider.GetRequiredService<IConnectionHostValidator>();
                var pinned = await hostValidator.ResolveAndValidateHostAsync(host, port, projectId, cancellationToken);

                Exception? lastException = null;
                foreach (var address in pinned.PinnedAddresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };
                    try
                    {
                        await socket.ConnectAsync(address, port, cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        socket.Dispose();
                        lastException = ex;
                    }
                }

                throw lastException ?? new SocketException((int)SocketError.HostUnreachable);
            }
        });

        return services;
    }
}
