using NSubstitute.ExceptionExtensions;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Commands.CreateDataset;

public sealed class CreateDatasetUseCaseTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDatasetResolver _datasetResolver = Substitute.For<IDatasetResolver>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();
    private readonly ProjectId _projectId = ProjectId.New();

    public CreateDatasetUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(_projectId);
    }

    private CreateDatasetUseCase CreateSut() => new(
        _datasetRepo,
        _unitOfWork,
        _datasetResolver,
        _projectContext,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WithInvalidCommand_ShouldThrowValidationException()
    {
        // Arrange
        var command = new CreateDatasetCommand("", null, Guid.Empty, "", -1);
        var pipeline = new ValidatingUseCaseDecorator<CreateDatasetCommand, DatasetResultDto>(
            CreateSut(),
            [new CreateDatasetCommandValidator()]);

        // Act
        Func<Task> act = () => pipeline.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<SmkDoc.Application.Common.Exceptions.ValidationException>();
        await _datasetResolver.DidNotReceive().ValidateAndResolveConnectionAsync(Arg.Any<DataConnectionId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _datasetRepo.DidNotReceive().AddAsync(Arg.Any<Dataset>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectUnresolved_ShouldThrowTenantContextRequiredException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.ProjectId.Returns(_ => throw new TenantContextRequiredException());
        var command = new CreateDatasetCommand("OrdersSet", null, Guid.NewGuid(), "SELECT 1");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<TenantContextRequiredException>();
        await _datasetResolver.DidNotReceive().ValidateAndResolveConnectionAsync(Arg.Any<DataConnectionId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldValidateReadOnlySqlPersistDatasetAndReturnDto()
    {
        // Arrange
        var connId = Guid.NewGuid();
        DataConnection conn = DataConnectionBuilder.SqlServer(name: "DB1", connectionString: "enc_db1", id: connId);
        var command = new CreateDatasetCommand("OrdersSet", "desc", connId, "SELECT * FROM orders", 60);

        _datasetResolver.ValidateAndResolveConnectionAsync(new DataConnectionId(connId), "SELECT * FROM orders", Arg.Any<CancellationToken>())
            .Returns(conn);

        // Act
        DatasetResultDto result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Name.Should().Be("OrdersSet");
        result.DataConnectionName.Should().Be("DB1");
        result.CacheSeconds.Should().Be(60);

        await _datasetResolver.Received(1).ValidateAndResolveConnectionAsync(new DataConnectionId(connId), "SELECT * FROM orders", Arg.Any<CancellationToken>());
        await _datasetRepo.Received(1).AddAsync(
            Arg.Is<Dataset>(d =>
                d.Name.Value == "OrdersSet" &&
                d.DataConnectionId.Value == connId &&
                d.CacheSeconds == 60),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _datasetResolver.ValidateAndResolveConnectionAsync(Arg.Any<DataConnectionId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotFoundException("DataConnection", Guid.NewGuid()));

        var command = new CreateDatasetCommand("OrdersSet", null, Guid.NewGuid(), "SELECT 1");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        await _datasetRepo.DidNotReceive().AddAsync(Arg.Any<Dataset>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
