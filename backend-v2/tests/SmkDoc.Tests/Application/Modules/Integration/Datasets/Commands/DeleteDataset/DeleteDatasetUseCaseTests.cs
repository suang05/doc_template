using SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Factories;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Commands.DeleteDataset;

public class DeleteDatasetUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private DeleteDatasetUseCase CreateSut() =>
        new(_fixture.DatasetRepo.Object, _fixture.UnitOfWork.Object);

    [Fact]
    public async Task ExecuteAsync_WhenFound_RemovesAndReturnsTrue()
    {
        // Arrange
        var entity = DatasetTestFactory.Create(name: "DS", dataConnectionId: Guid.NewGuid(), sqlQuery: "SELECT 1", cacheSeconds: 0);

        _fixture.DatasetRepo
            .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _fixture.UnitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new DeleteDatasetCommand(entity.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.Should().BeTrue();
        _fixture.DatasetRepo.Verify(r => r.Remove(entity), Times.Once);
        _fixture.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.Should().BeFalse();
        _fixture.DatasetRepo.Verify(r => r.Remove(It.IsAny<Dataset>()), Times.Never);
        _fixture.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
