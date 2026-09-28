using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Modules.Integration.DataConnections;
using SmkDoc.Application.Modules.Integration.Datasets;

namespace SmkDoc.Application.Modules.Integration;

public static class IntegrationModuleExtensions
{
    public static IServiceCollection AddIntegrationModule(this IServiceCollection services)
    {
        // External Data Connections & Datasets Pipeline
        services.AddScoped<DataConnectionUseCase>();
        services.AddScoped<DatasetUseCase>();

        return services;
    }
}
