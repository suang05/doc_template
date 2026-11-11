using NSubstitute.ExceptionExtensions;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Commands.UpdateDataset;

public sealed class UpdateDatasetUseCaseTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDatasetResolver _datasetResolver = Substitute.For<IDatasetResolver>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public UpdateDatasetUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private UpdateDatasetUseCase CreateSut() => new(
        _datasetRepo,
        _unitOfWork,
        _datasetResolver,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WithInvalidCommand_ShouldThrowValidationException()
    {
        // Arrange
        var command = new UpdateDatasetCommand(Guid.Empty, "", null, Guid.Empty, "", -1);
        var pipeline = new ValidatingUseCaseDecorator<UpdateDatasetCommand, DatasetResultDto>(
            CreateSut(),
            [new UpdateDatasetCommandValidator()]);

        // Act
        Func<Task> act = () => pipeline.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<SmkDoc.Application.Common.Exceptions.ValidationException>();
        await _datasetResolver.DidNotReceive().ValidateAndResolveConnectionAsync(Arg.Any<DataConnectionId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _datasetRepo.DidNotReceive().GetByIdAsync(Arg.Any<DatasetId>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFound_ValidatesReadOnlySqlUpdatesAndCommits()
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

        _datasetResolver.ValidateAndResolveConnectionAsync(new DataConnectionId(connId), "SELECT 2", Arg.Any<CancellationToken>())
            .Returns(conn);

        _datasetRepo.GetByIdAsync(new DatasetId(id), Arg.Any<CancellationToken>())
            .Returns(dataset);

        var command = new UpdateDatasetCommand(id, "NewName", "new desc", connId, "SELECT 2", 60);

        // Act
        DatasetResultDto? result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("NewName");
        result.DataConnectionName.Should().Be("Conn1");
        result.CacheSeconds.Should().Be(60);
        await _datasetResolver.Received(1).ValidateAndResolveConnectionAsync(new DataConnectionId(connId), "SELECT 2", Arg.Any<CancellationToken>());
        _datasetRepo.Received(1).Update(dataset);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEntityNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var connId = Guid.NewGuid();
        _datasetResolver.ValidateAndResolveConnectionAsync(new DataConnectionId(connId), "SELECT 1", Arg.Any<CancellationToken>())
            .Returns(DataConnectionBuilder.Postgres(id: connId));

        _datasetRepo.GetByIdAsync(new DatasetId(id), Arg.Any<CancellationToken>())
            .Returns((Dataset?)null);

        var command = new UpdateDatasetCommand(id, "Name", null, connId, "SELECT 1");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _datasetRepo.DidNotReceive().Update(Arg.Any<Dataset>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _datasetResolver.ValidateAndResolveConnectionAsync(Arg.Any<DataConnectionId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotFoundException("DataConnection", Guid.NewGuid()));

        var command = new UpdateDatasetCommand(id, "Name", null, Guid.NewGuid(), "SELECT 1");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        await _datasetRepo.DidNotReceive().GetByIdAsync(Arg.Any<DatasetId>(), Arg.Any<CancellationToken>());
        _datasetRepo.DidNotReceive().Update(Arg.Any<Dataset>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
