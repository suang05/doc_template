using SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;

public class UpdateDataConnectionUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private UpdateDataConnectionUseCase CreateSut() => _fixture.BuildUpdateDataConnectionUseCase();

    [Fact]
    public async Task ExecuteAsync_WhenFoundWithNewSecret_EncryptsAndUpdates()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = DataConnectionBuilder.Postgres(name: "Old Name", connectionString: "old_enc", id: id);

        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _fixture.DataProtection
            .Setup(p => p.Encrypt("new_plain_conn"))
            .Returns("new_enc_conn");

        var command = new UpdateDataConnectionCommand(id, "New Name", "SqlServer", "new_plain_conn");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        result.Provider.Should().Be("SqlServer");
        existing.EncryptedConnectionString.Should().Be("new_enc_conn");
        existing.UpdatedAt.Should().NotBeNull();
        _fixture.ConnectionRepo.Verify(r => r.Update(existing), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionStringIsEmpty_PreservesExistingSecret()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = DataConnectionBuilder.Postgres(name: "DB", connectionString: "keep_this_enc", id: id);

        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new UpdateDataConnectionCommand(id, "DB Renamed", "PostgreSQL", "");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        existing.EncryptedConnectionString.Should().Be("keep_this_enc");
        _fixture.DataProtection.Verify(p => p.Encrypt(It.IsAny<string>()), Times.Never);
        _fixture.ConnectionRepo.Verify(r => r.Update(existing), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEntityNotFound_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataConnection?)null);

        var command = new UpdateDataConnectionCommand(id, "X", "PostgreSQL", "conn");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeNull();
    }
}
