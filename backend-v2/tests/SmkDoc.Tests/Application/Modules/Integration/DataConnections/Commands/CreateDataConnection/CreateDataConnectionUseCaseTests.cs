using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Application.Modules.Integration.Datasets.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;

public sealed class CreateDataConnectionUseCaseTests
{
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDatasetResolver _datasetResolver = Substitute.For<IDatasetResolver>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();
    private readonly ProjectId _projectId = ProjectId.New();

    public CreateDataConnectionUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _datasetResolver.ValidateAndEncryptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromResult(EncryptedConnectionString.Create($"enc:{x.ArgAt<string>(1)}")));
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(_projectId);
    }

    private CreateDataConnectionUseCase CreateSut() => new(
        _connectionRepo,
        _unitOfWork,
        _datasetResolver,
        _projectContext,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WithInvalidCommand_ShouldThrowValidationException()
    {
        // Arrange
        var command = new CreateDataConnectionCommand("", "InvalidProvider", "");
        var pipeline = new ValidatingUseCaseDecorator<CreateDataConnectionCommand, DataConnectionResultDto>(
            CreateSut(),
            [new CreateDataConnectionCommandValidator()]);

        // Act
        Func<Task> act = () => pipeline.ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _connectionRepo.DidNotReceive().AddAsync(Arg.Any<DataConnection>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectUnresolved_ShouldThrowTenantContextNotResolvedException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.GetRequiredProjectId().Returns(_ => throw new TenantContextNotResolvedException());
        var command = new CreateDataConnectionCommand("Warehouse", "PostgreSQL", "Host=10.0.0.5;Database=erp");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TenantContextNotResolvedException>();
        await _datasetResolver.DidNotReceive().ValidateAndEncryptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldValidateConnectionTargetEncryptAndPersist()
    {
        // Arrange
        var command = new CreateDataConnectionCommand("Warehouse", "PostgreSQL", "Host=wh.local;Pass=secret");

        // Act
        DataConnectionResultDto result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be("Warehouse");
        result.Provider.Should().Be("PostgreSQL");

        await _datasetResolver.Received(1).ValidateAndEncryptAsync("PostgreSQL", "Host=wh.local;Pass=secret", Arg.Any<CancellationToken>());
        await _connectionRepo.Received(1).AddAsync(
            Arg.Is<DataConnection>(c =>
                c.Name.Value == "Warehouse" &&
                c.Provider == DatabaseProvider.PostgreSQL &&
                c.EncryptedConnectionString == "enc:Host=wh.local;Pass=secret"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
