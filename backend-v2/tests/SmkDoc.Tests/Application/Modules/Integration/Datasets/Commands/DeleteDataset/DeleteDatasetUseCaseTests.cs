using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
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
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    public DeleteDatasetUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(ProjectId.New());
    }

    private DeleteDatasetUseCase CreateSut() => new(
        _datasetRepo,
        _unitOfWork,
        _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextNotResolved_ThrowsForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        var command = new DeleteDatasetCommand(Guid.NewGuid());

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _datasetRepo.DidNotReceive().Remove(Arg.Any<Dataset>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFound_RemovesAndCommits()
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
        await CreateSut().ExecuteAsync(command);

        // Assert
        _datasetRepo.Received(1).Remove(entity);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _datasetRepo.GetByIdAsync(new DatasetId(id), Arg.Any<CancellationToken>())
            .Returns((Dataset?)null);

        var command = new DeleteDatasetCommand(id);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _datasetRepo.DidNotReceive().Remove(Arg.Any<Dataset>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
