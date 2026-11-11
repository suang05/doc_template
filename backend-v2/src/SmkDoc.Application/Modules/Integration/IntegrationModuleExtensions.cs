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
using SmkDoc.Application.Modules.Integration.Datasets.Commands.DryRunDataset;
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
        services.AddUseCase<CreateDatasetCommand, DatasetResultDto, CreateDatasetUseCase>();
        services.AddUseCase<UpdateDatasetCommand, DatasetResultDto, UpdateDatasetUseCase>();
        services.AddUseCase<DeleteDatasetCommand, DeleteDatasetUseCase>();
        services.AddUseCase<GetDatasetByIdQuery, DatasetResultDto, GetDatasetByIdUseCase>();
        services.AddUseCase<ListDatasetsQuery, List<DatasetResultDto>, ListDatasetsUseCase>();
        services.AddUseCase<DryRunDatasetCommand, DryRunDatasetResultDto, DryRunDatasetUseCase>();

        // Single-Responsibility UseCases - DataConnections
        services.AddUseCase<CreateDataConnectionCommand, DataConnectionResultDto, CreateDataConnectionUseCase>();
        services.AddUseCase<UpdateDataConnectionCommand, DataConnectionResultDto, UpdateDataConnectionUseCase>();
        services.AddUseCase<DeleteDataConnectionCommand, DeleteDataConnectionUseCase>();
        services.AddUseCase<TestDataConnectionCommand, TestDataConnectionResultDto, TestDataConnectionUseCase>();
        services.AddUseCase<GetDataConnectionByIdQuery, DataConnectionResultDto, GetDataConnectionByIdUseCase>();
        services.AddUseCase<ListDataConnectionsQuery, List<DataConnectionResultDto>, ListDataConnectionsUseCase>();

        return services;
    }
}
