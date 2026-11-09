using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Infrastructure.Extensions;

namespace SmkDoc.Infrastructure;

/// <summary>
/// Composition root for SmkDoc.Infrastructure layer.
/// Coordinates subsystem installers adhering to strict Single Responsibility &amp; Modular DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        return services
            .AddSecurityInfrastructure()
            .AddPersistenceInfrastructure(configuration)
            .AddStorageInfrastructure(configuration)
            .AddRenderingEnginesInfrastructure(configuration)
            .AddDistributedInfrastructure(configuration);
    }
}
