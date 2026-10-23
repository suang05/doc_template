using SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;

public sealed class DeleteDataConnectionUseCaseTests
{
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public DeleteDataConnectionUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private DeleteDataConnectionUseCase CreateSut() => new(
        _connectionRepo,
        _unitOfWork);

    [Fact]
    public async Task ExecuteAsync_WhenFound_RemovesAndReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        DataConnection existing = DataConnectionBuilder.Postgres(name: "To Delete", connectionString: "enc_delete", id: id);

        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns(existing);

        var command = new DeleteDataConnectionCommand(id);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeTrue();
        _connectionRepo.Received(1).Remove(existing);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns((DataConnection?)null);

        var command = new DeleteDataConnectionCommand(id);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeFalse();
        _connectionRepo.DidNotReceive().Remove(Arg.Any<DataConnection>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
