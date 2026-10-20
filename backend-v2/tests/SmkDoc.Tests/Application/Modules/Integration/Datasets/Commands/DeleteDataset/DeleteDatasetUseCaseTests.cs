using SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Commands.DeleteDataset;

public class DeleteDatasetUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private DeleteDatasetUseCase CreateSut() => _fixture.BuildDeleteDatasetUseCase();

    [Fact]
    public async Task ExecuteAsync_WhenFound_RemovesAndReturnsTrue()
    {
        // Arrange
        var entity = new DatasetBuilder().WithName("DS").WithDataConnectionId(Guid.NewGuid()).WithSqlQuery("SELECT 1").WithCacheSeconds(0).Build();

        _fixture.DatasetRepo
            .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var command = new DeleteDatasetCommand(entity.Id);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeTrue();
        _fixture.DatasetRepo.Verify(r => r.Remove(entity), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        _fixture.DatasetRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dataset?)null);

        var command = new DeleteDatasetCommand(id);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeFalse();
        _fixture.DatasetRepo.Verify(r => r.Remove(It.IsAny<Dataset>()), Times.Never);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
