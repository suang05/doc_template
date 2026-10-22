using SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Commands.CreateDataset;

public sealed class CreateDatasetUseCaseTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public CreateDatasetUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private CreateDatasetUseCase CreateSut() => new(
        _datasetRepo,
        _connectionRepo,
        _unitOfWork);

    [Fact]
    public async Task ExecuteAsync_ShouldPersistDatasetAndReturnDto()
    {
        // Arrange
        var connId = Guid.NewGuid();
        DataConnection conn = DataConnectionBuilder.SqlServer(name: "DB1", connectionString: "enc_db1", id: connId);
        var command = new CreateDatasetCommand("OrdersSet", "desc", connId, "SELECT * FROM orders", 60);

        _connectionRepo.GetByIdAsync(connId, Arg.Any<CancellationToken>())
            .Returns(conn);

        // Act
        DatasetDto result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Name.Should().Be("OrdersSet");
        result.DataConnectionName.Should().Be("DB1");
        result.CacheSeconds.Should().Be(60);

        await _datasetRepo.Received(1).AddAsync(
            Arg.Is<Dataset>(d =>
                d.Name.Value == "OrdersSet" &&
                d.DataConnectionId == connId &&
                d.CacheSeconds == 60),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _connectionRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((DataConnection?)null);

        var command = new CreateDatasetCommand("OrdersSet", null, Guid.NewGuid(), "SELECT 1");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        await _datasetRepo.DidNotReceive().AddAsync(Arg.Any<Dataset>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
