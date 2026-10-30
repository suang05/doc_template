using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Application.Modules.Integration.DataConnections.Validators;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;

public sealed class CreateDataConnectionUseCaseTests
{
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDataProtectionService _dataProtection = Substitute.For<IDataProtectionService>();
    private readonly ISqlConnectionValidator _sqlValidator = Substitute.For<ISqlConnectionValidator>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ProjectId _projectId = ProjectId.New();

    public CreateDataConnectionUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _dataProtection.Encrypt(Arg.Any<string>()).Returns(x => $"enc:{x.Arg<string>()}");
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(_projectId);
    }

    private CreateDataConnectionUseCase CreateSut(IValidator<CreateDataConnectionCommand>? validator = null) => new(
        _connectionRepo,
        _unitOfWork,
        _dataProtection,
        validator ?? new CreateDataConnectionCommandValidator(),
        _sqlValidator,
        _projectContext);

    [Fact]
    public async Task ExecuteAsync_WithInvalidCommand_ShouldThrowValidationException()
    {
        // Arrange
        var command = new CreateDataConnectionCommand("", "InvalidProvider", "");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<SmkDoc.Application.Common.Exceptions.ValidationException>();
        await _connectionRepo.DidNotReceive().AddAsync(Arg.Any<DataConnection>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectUnresolved_ShouldThrowForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        var command = new CreateDataConnectionCommand("Warehouse", "PostgreSQL", "Host=10.0.0.5;Database=erp");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        await _sqlValidator.DidNotReceive().ValidateConnectionTargetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldValidateConnectionTargetEncryptAndPersist()
    {
        // Arrange
        var command = new CreateDataConnectionCommand("Warehouse", "PostgreSQL", "Host=wh.local;Pass=secret");

        // Act
        DataConnectionDto result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Name.Should().Be("Warehouse");
        result.Provider.Should().Be("PostgreSQL");

        await _sqlValidator.Received(1).ValidateConnectionTargetAsync("PostgreSQL", "Host=wh.local;Pass=secret", Arg.Any<CancellationToken>());
        await _connectionRepo.Received(1).AddAsync(
            Arg.Is<DataConnection>(c =>
                c.Name.Value == "Warehouse" &&
                c.Provider == DatabaseProvider.PostgreSQL &&
                c.EncryptedConnectionString == "enc:Host=wh.local;Pass=secret"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
