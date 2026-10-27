using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Application.Modules.Integration.DataConnections.Validators;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;

public sealed class UpdateDataConnectionUseCaseTests
{
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDataProtectionService _dataProtection = Substitute.For<IDataProtectionService>();

    public UpdateDataConnectionUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private UpdateDataConnectionUseCase CreateSut(IValidator<UpdateDataConnectionCommand>? validator = null) => new(
        _connectionRepo,
        _unitOfWork,
        _dataProtection,
        validator ?? new UpdateDataConnectionCommandValidator());

    [Fact]
    public async Task ExecuteAsync_WithInvalidCommand_ShouldThrowValidationException()
    {
        // Arrange
        var command = new UpdateDataConnectionCommand(Guid.Empty, "", "InvalidProvider", "");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<SmkDoc.Application.Common.Exceptions.ValidationException>();
        await _connectionRepo.DidNotReceive().GetByIdAsync(Arg.Any<DataConnectionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFoundWithNewSecret_EncryptsAndUpdates()
    {
        // Arrange
        var id = Guid.NewGuid();
        DataConnection existing = DataConnectionBuilder.Postgres(name: "Old Name", connectionString: "old_enc", id: id);

        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns(existing);

        _dataProtection.Encrypt("new_plain_conn")
            .Returns("new_enc_conn");

        var command = new UpdateDataConnectionCommand(id, "New Name", "SqlServer", "new_plain_conn");

        // Act
        DataConnectionDto? result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        result.Provider.Should().Be("SqlServer");
        existing.EncryptedConnectionString.Value.Should().Be("new_enc_conn");
        existing.UpdatedAt.Should().NotBeNull();

        _connectionRepo.Received(1).Update(existing);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionStringIsEmpty_PreservesExistingSecret()
    {
        // Arrange
        var id = Guid.NewGuid();
        DataConnection existing = DataConnectionBuilder.Postgres(name: "DB", connectionString: "keep_this_enc", id: id);

        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns(existing);

        var command = new UpdateDataConnectionCommand(id, "DB Renamed", "PostgreSQL", "");

        // Act
        DataConnectionDto? result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        existing.EncryptedConnectionString.Value.Should().Be("keep_this_enc");
        _dataProtection.DidNotReceive().Encrypt(Arg.Any<string>());
        _connectionRepo.Received(1).Update(existing);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEntityNotFound_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns((DataConnection?)null);

        var command = new UpdateDataConnectionCommand(id, "X", "PostgreSQL", "conn");

        // Act
        DataConnectionDto? result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeNull();
        _connectionRepo.DidNotReceive().Update(Arg.Any<DataConnection>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
