using SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;

public class DeleteDataConnectionUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private DeleteDataConnectionUseCase CreateSut() => _fixture.BuildDeleteDataConnectionUseCase();

    [Fact]
    public async Task ExecuteAsync_WhenFound_RemovesAndReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = DataConnectionBuilder.Postgres(name: "To Delete", connectionString: "enc_delete", id: id);

        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new DeleteDataConnectionCommand(id);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeTrue();
        _fixture.ConnectionRepo.Verify(r => r.Remove(existing), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataConnection?)null);

        var command = new DeleteDataConnectionCommand(id);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeFalse();
        _fixture.ConnectionRepo.Verify(r => r.Remove(It.IsAny<DataConnection>()), Times.Never);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
