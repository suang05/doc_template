namespace SmkDoc.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPresentationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddApiProblemDetails(configuration);
        services.AddApiControllers();
        services.AddApiCors(configuration);
        services.AddApiAuthentication(configuration);
        services.AddApiOpenApi();
        services.AddApiRateLimiting(configuration);
        services.AddApiHealthChecks(configuration);

        return services;
    }
}
