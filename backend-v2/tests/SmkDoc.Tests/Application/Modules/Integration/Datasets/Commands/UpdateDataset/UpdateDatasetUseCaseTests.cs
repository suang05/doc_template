using SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Factories;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Commands.UpdateDataset;

public class UpdateDatasetUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private UpdateDatasetUseCase CreateSut() =>
        new(_fixture.DatasetRepo.Object, _fixture.ConnectionRepo.Object, _fixture.UnitOfWork.Object);

    [Fact]
    public async Task ExecuteAsync_WhenFound_UpdatesAndCommits()
    {
        // Arrange
        var id = Guid.NewGuid();
        var connId = Guid.NewGuid();
        var conn = DataConnectionTestFactory.Create(connId, "Conn1", DatabaseProvider.PostgreSQL, "enc_conn");
        var dataset = DatasetTestFactory.Create(id, "OldName", "old desc", connId, "SELECT 1", 0);

        _fixture.DatasetRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataset);

        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(connId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conn);

        _fixture.UnitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new UpdateDatasetCommand(id, "NewName", "new desc", connId, "SELECT 2", 60);
        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("NewName");
        result.DataConnectionName.Should().Be("Conn1");
        result.CacheSeconds.Should().Be(60);
        _fixture.DatasetRepo.Verify(r => r.Update(dataset), Times.Once);
        _fixture.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEntityNotFound_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _fixture.DatasetRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dataset?)null);

        var command = new UpdateDatasetCommand(id, "Name", null, Guid.NewGuid(), "SELECT 1");
        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dataset = DatasetTestFactory.Create(id, "Name", null, Guid.NewGuid(), "SELECT 1", 0);

        _fixture.DatasetRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataset);

        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataConnection?)null);

        var command = new UpdateDatasetCommand(id, "Name", null, Guid.NewGuid(), "SELECT 1");
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
