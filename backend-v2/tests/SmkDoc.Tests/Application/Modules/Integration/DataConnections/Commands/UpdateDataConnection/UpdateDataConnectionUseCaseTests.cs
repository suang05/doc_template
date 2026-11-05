using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
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
    private readonly ISqlConnectionValidator _sqlValidator = Substitute.For<ISqlConnectionValidator>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public UpdateDataConnectionUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private UpdateDataConnectionUseCase CreateSut() => new(
        _connectionRepo,
        _unitOfWork,
        _dataProtection,
        _sqlValidator,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WithInvalidCommand_ShouldThrowValidationException()
    {
        // Arrange
        var command = new UpdateDataConnectionCommand(Guid.Empty, "", "InvalidProvider", "");
        var pipeline = new ValidatingUseCaseDecorator<UpdateDataConnectionCommand, DataConnectionResultDto>(
            CreateSut(),
            [new UpdateDataConnectionCommandValidator()]);

        // Act
        Func<Task> act = () => pipeline.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<SmkDoc.Application.Common.Exceptions.ValidationException>();
        await _connectionRepo.DidNotReceive().GetByIdAsync(Arg.Any<DataConnectionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFoundWithNewSecret_ValidatesTargetEncryptsAndUpdates()
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
        DataConnectionResultDto? result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        result.Provider.Should().Be("SqlServer");
        existing.EncryptedConnectionString.Value.Should().Be("new_enc_conn");
        existing.UpdatedAt.Should().NotBeNull();

        await _sqlValidator.Received(1).ValidateConnectionTargetAsync("SqlServer", "new_plain_conn", Arg.Any<CancellationToken>());
        _connectionRepo.Received(1).Update(existing);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionStringIsEmpty_ValidatesDecryptedTargetAndPreservesExistingSecret()
    {
        // Arrange
        var id = Guid.NewGuid();
        DataConnection existing = DataConnectionBuilder.Postgres(name: "DB", connectionString: "keep_this_enc", id: id);

        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns(existing);
        _dataProtection.Decrypt("keep_this_enc").Returns("Host=10.0.0.10;Database=erp");

        var command = new UpdateDataConnectionCommand(id, "DB Renamed", "PostgreSQL", "");

        // Act
        DataConnectionResultDto? result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        existing.EncryptedConnectionString.Value.Should().Be("keep_this_enc");
        await _sqlValidator.Received(1).ValidateConnectionTargetAsync("PostgreSQL", "Host=10.0.0.10;Database=erp", Arg.Any<CancellationToken>());
        _dataProtection.DidNotReceive().Encrypt(Arg.Any<string>());
        _connectionRepo.Received(1).Update(existing);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEntityNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns((DataConnection?)null);

        var command = new UpdateDataConnectionCommand(id, "X", "PostgreSQL", "conn");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _connectionRepo.DidNotReceive().Update(Arg.Any<DataConnection>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIfMatchValid_UpdatesAndReturnsConcurrencyToken()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = DataConnectionBuilder.Postgres(name: "DB", connectionString: "enc", id: id);
        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns(existing);

        var expectedToken = (existing.UpdatedAt ?? existing.CreatedAt).ToUnixTimeMilliseconds().ToString();
        var command = new UpdateDataConnectionCommand(id, "DB Renamed", "PostgreSQL", "", $"\"{expectedToken}\"");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.ConcurrencyToken.Should().NotBeNullOrEmpty();
        _connectionRepo.Received(1).Update(existing);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIfMatchWildcard_UpdatesSuccessfully()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = DataConnectionBuilder.Postgres(name: "DB", connectionString: "enc", id: id);
        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns(existing);

        var command = new UpdateDataConnectionCommand(id, "DB Renamed", "PostgreSQL", "", "*");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        _connectionRepo.Received(1).Update(existing);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIfMatchMismatch_ThrowsPreconditionFailedException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = DataConnectionBuilder.Postgres(name: "DB", connectionString: "enc", id: id);
        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns(existing);

        var command = new UpdateDataConnectionCommand(id, "DB Renamed", "PostgreSQL", "", "\"stale_token_123\"");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<PreconditionFailedException>()
            .WithMessage("*Precondition failed for data connection*");
        _connectionRepo.DidNotReceive().Update(Arg.Any<DataConnection>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
