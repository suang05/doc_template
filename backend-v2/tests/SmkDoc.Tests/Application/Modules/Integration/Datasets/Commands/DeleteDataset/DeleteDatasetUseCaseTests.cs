using SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Commands.DeleteDataset;

public sealed class DeleteDatasetUseCaseTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public DeleteDatasetUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private DeleteDatasetUseCase CreateSut() => new(
        _datasetRepo,
        _unitOfWork);

    [Fact]
    public async Task ExecuteAsync_WhenFound_RemovesAndReturnsTrue()
    {
        // Arrange
        Dataset entity = new DatasetBuilder()
            .WithName("DS")
            .WithDataConnectionId(Guid.NewGuid())
            .WithSqlQuery("SELECT 1")
            .WithCacheSeconds(0);

        _datasetRepo.GetByIdAsync(entity.Id, Arg.Any<CancellationToken>())
            .Returns(entity);

        var command = new DeleteDatasetCommand(entity.Id.Value);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeTrue();
        _datasetRepo.Received(1).Remove(entity);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        _datasetRepo.GetByIdAsync(new DatasetId(id), Arg.Any<CancellationToken>())
            .Returns((Dataset?)null);

        var command = new DeleteDatasetCommand(id);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeFalse();
        _datasetRepo.DidNotReceive().Remove(Arg.Any<Dataset>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
