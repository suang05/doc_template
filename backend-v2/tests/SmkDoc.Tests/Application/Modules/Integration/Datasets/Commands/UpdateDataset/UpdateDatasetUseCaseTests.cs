using SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Commands.UpdateDataset;

public sealed class UpdateDatasetUseCaseTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public UpdateDatasetUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private UpdateDatasetUseCase CreateSut() => new(
        _datasetRepo,
        _connectionRepo,
        _unitOfWork);

    [Fact]
    public async Task ExecuteAsync_WhenFound_UpdatesAndCommits()
    {
        // Arrange
        var id = Guid.NewGuid();
        var connId = Guid.NewGuid();
        DataConnection conn = DataConnectionBuilder.Postgres(name: "Conn1", connectionString: "enc_conn", id: connId);
        Dataset dataset = new DatasetBuilder()
            .WithId(id)
            .WithName("OldName")
            .WithDescription("old desc")
            .WithDataConnectionId(connId)
            .WithSqlQuery("SELECT 1")
            .WithCacheSeconds(0);

        _datasetRepo.GetByIdAsync(new DatasetId(id), Arg.Any<CancellationToken>())
            .Returns(dataset);

        _connectionRepo.GetByIdAsync(new DataConnectionId(connId), Arg.Any<CancellationToken>())
            .Returns(conn);

        var command = new UpdateDatasetCommand(id, "NewName", "new desc", connId, "SELECT 2", 60);

        // Act
        DatasetDto? result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("NewName");
        result.DataConnectionName.Should().Be("Conn1");
        result.CacheSeconds.Should().Be(60);
        _datasetRepo.Received(1).Update(dataset);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEntityNotFound_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _datasetRepo.GetByIdAsync(new DatasetId(id), Arg.Any<CancellationToken>())
            .Returns((Dataset?)null);

        var command = new UpdateDatasetCommand(id, "Name", null, Guid.NewGuid(), "SELECT 1");

        // Act
        DatasetDto? result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeNull();
        _datasetRepo.DidNotReceive().Update(Arg.Any<Dataset>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        Dataset dataset = new DatasetBuilder()
            .WithId(id)
            .WithName("Name")
            .WithDataConnectionId(Guid.NewGuid())
            .WithSqlQuery("SELECT 1")
            .WithCacheSeconds(0);

        _datasetRepo.GetByIdAsync(new DatasetId(id), Arg.Any<CancellationToken>())
            .Returns(dataset);

        _connectionRepo.GetByIdAsync(Arg.Any<DataConnectionId>(), Arg.Any<CancellationToken>())
            .Returns((DataConnection?)null);

        var command = new UpdateDatasetCommand(id, "Name", null, Guid.NewGuid(), "SELECT 1");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _datasetRepo.DidNotReceive().Update(Arg.Any<Dataset>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
