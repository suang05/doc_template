using Microsoft.Extensions.DependencyInjection;
namespace SmkDoc.Application.Modules.Integration;

public static class IntegrationModuleExtensions
{
    public static IServiceCollection AddIntegrationModule(this IServiceCollection services)
    {

        // Single-Responsibility UseCases - Datasets
        services.AddScoped<SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset.CreateDatasetUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset.UpdateDatasetUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset.DeleteDatasetUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById.GetDatasetByIdUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets.ListDatasetsUseCase>();

        // Single-Responsibility UseCases - DataConnections
        services.AddScoped<SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection.CreateDataConnectionUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection.UpdateDataConnectionUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection.DeleteDataConnectionUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection.TestDataConnectionUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById.GetDataConnectionByIdUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections.ListDataConnectionsUseCase>();

        return services;
    }
}
