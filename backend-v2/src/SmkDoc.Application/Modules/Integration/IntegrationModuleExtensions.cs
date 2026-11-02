using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets;

namespace SmkDoc.Application.Modules.Integration;

public static class IntegrationModuleExtensions
{
    public static IServiceCollection AddIntegrationModule(this IServiceCollection services)
    {
        // Single-Responsibility UseCases - Datasets
        services.AddUseCase<CreateDatasetCommand, DatasetDto, CreateDatasetUseCase>();
        services.AddUseCase<UpdateDatasetCommand, DatasetDto?, UpdateDatasetUseCase>();
        services.AddUseCase<DeleteDatasetCommand, bool, DeleteDatasetUseCase>();
        services.AddUseCase<GetDatasetByIdQuery, DatasetDto?, GetDatasetByIdUseCase>();
        services.AddUseCase<ListDatasetsQuery, List<DatasetDto>, ListDatasetsUseCase>();

        // Single-Responsibility UseCases - DataConnections
        services.AddUseCase<CreateDataConnectionCommand, DataConnectionDto, CreateDataConnectionUseCase>();
        services.AddUseCase<UpdateDataConnectionCommand, DataConnectionDto?, UpdateDataConnectionUseCase>();
        services.AddUseCase<DeleteDataConnectionCommand, bool, DeleteDataConnectionUseCase>();
        services.AddUseCase<TestDataConnectionCommand, bool, TestDataConnectionUseCase>();
        services.AddUseCase<GetDataConnectionByIdQuery, DataConnectionDto?, GetDataConnectionByIdUseCase>();
        services.AddUseCase<ListDataConnectionsQuery, List<DataConnectionDto>, ListDataConnectionsUseCase>();

        return services;
    }
}
