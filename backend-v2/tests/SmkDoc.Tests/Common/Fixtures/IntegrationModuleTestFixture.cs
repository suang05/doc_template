using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Common.Fixtures;

/// <summary>
/// SSoT test fixture centralizing mocks and factory methods for the Integration Module (Datasets, DataConnections).
/// Adheres to anti-bloat test guidelines (RULE-TEST-007).
/// </summary>
public class IntegrationModuleTestFixture
{
    public Mock<IDatasetRepository> DatasetRepo { get; } = new();
    public Mock<IDataConnectionRepository> ConnectionRepo { get; } = new();
    public Mock<IUnitOfWork> Uow { get; } = new();
    public Mock<IDataProtectionService> DataProtection { get; } = new();
    public Mock<ISqlExecutorService> SqlExecutor { get; } = new();

    public IntegrationModuleTestFixture()
    {
        Uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        DataProtection.Setup(p => p.Encrypt(It.IsAny<string>())).Returns<string>(s => $"enc:{s}");
        DataProtection.Setup(p => p.Decrypt(It.IsAny<string>())).Returns<string>(s => s.Replace("enc:", ""));
    }

    public CreateDatasetUseCase BuildCreateDatasetUseCase() =>
        new(DatasetRepo.Object, ConnectionRepo.Object, Uow.Object);

    public UpdateDatasetUseCase BuildUpdateDatasetUseCase() =>
        new(DatasetRepo.Object, ConnectionRepo.Object, Uow.Object);

    public DeleteDatasetUseCase BuildDeleteDatasetUseCase() =>
        new(DatasetRepo.Object, Uow.Object);

    public GetDatasetByIdUseCase BuildGetDatasetByIdUseCase() =>
        new(DatasetRepo.Object, ConnectionRepo.Object);

    public ListDatasetsUseCase BuildListDatasetsUseCase() =>
        new(DatasetRepo.Object, ConnectionRepo.Object);

    public CreateDataConnectionUseCase BuildCreateDataConnectionUseCase() =>
        new(ConnectionRepo.Object, Uow.Object, DataProtection.Object);

    public UpdateDataConnectionUseCase BuildUpdateDataConnectionUseCase() =>
        new(ConnectionRepo.Object, Uow.Object, DataProtection.Object);

    public DeleteDataConnectionUseCase BuildDeleteDataConnectionUseCase() =>
        new(ConnectionRepo.Object, Uow.Object);

    public GetDataConnectionByIdUseCase BuildGetDataConnectionByIdUseCase() =>
        new(ConnectionRepo.Object);

    public ListDataConnectionsUseCase BuildListDataConnectionsUseCase() =>
        new(ConnectionRepo.Object);

    public TestDataConnectionUseCase BuildTestDataConnectionUseCase() =>
        new(SqlExecutor.Object);
}
