using SmkDoc.Api.HealthChecks;
using SmkDoc.Api.OpenApi.Transformers;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Api.Extensions;

internal static class ApiObservabilityExtensions
{
    internal static void AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<OpenApiDocumentTransformer>();
            options.AddOperationTransformer<OpenApiOperationTransformer>();
        });
    }

    internal static void AddApiHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthChecks = services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgres", tags: ["db"])
            .AddCheck<GotenbergHealthCheck>("gotenberg", tags: ["deps"])
            .AddCheck<MinioHealthCheck>("minio", tags: ["deps"]);

        if (RedisConfigurationResolver.ResolveConnectionString(configuration) is not null)
        {
            healthChecks.AddCheck<RedisHealthCheck>("redis", tags: ["deps"]);
        }
    }
}
