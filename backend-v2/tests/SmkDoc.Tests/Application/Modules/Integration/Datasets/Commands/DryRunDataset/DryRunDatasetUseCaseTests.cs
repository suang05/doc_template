using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.DryRunDataset;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Commands.DryRunDataset;

public sealed class DryRunDatasetUseCaseTests
{
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IDataProtectionService _dataProtection = Substitute.For<IDataProtectionService>();
    private readonly ISqlConnectionValidator _sqlValidator = Substitute.For<ISqlConnectionValidator>();
    private readonly ISqlDriverFactory _driverFactory = Substitute.For<ISqlDriverFactory>();
    private readonly ISqlDriverStrategy _driverStrategy = Substitute.For<ISqlDriverStrategy>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    public DryRunDatasetUseCaseTests()
    {
        _driverStrategy.ProviderName.Returns("PostgreSQL");
        _driverFactory.GetStrategy(Arg.Any<string>()).Returns(_driverStrategy);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(ProjectId.New());
    }

    private DryRunDatasetUseCase CreateSut() => new(
        _connectionRepo,
        _dataProtection,
        _sqlValidator,
        _driverFactory,
        _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenProjectUnresolved_ThrowsForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        var command = new DryRunDatasetCommand(Guid.NewGuid(), null, null, "SELECT 1");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        await _connectionRepo.DidNotReceive().GetByIdAsync(Arg.Any<DataConnectionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithSavedConnection_ResolvesDecryptsAndExecutesDryRun()
    {
        // Arrange
        var connId = Guid.NewGuid();
        DataConnection connection = DataConnectionBuilder.Postgres(
            name: "ERP DB",
            connectionString: "enc_conn_str",
            id: connId);

        _connectionRepo.GetByIdAsync(new DataConnectionId(connId), Arg.Any<CancellationToken>())
            .Returns(connection);
        _dataProtection.Decrypt("enc_conn_str").Returns("Host=remote;Database=erp");

        var columns = new List<DatasetColumnSchemaDto>
        {
            new("id", "integer", false),
            new("total", "numeric", true)
        };
        var sampleRows = new List<IDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["id"] = 1, ["total"] = 150.5m }
        };

        _driverStrategy.ExecuteDryRunAsync(
            "Host=remote;Database=erp",
            "SELECT id, total FROM orders",
            Arg.Any<IDictionary<string, object>?>(),
            5,
            15,
            Arg.Any<CancellationToken>())
            .Returns(new DryRunExecutionResult(columns, sampleRows, 12));

        var command = new DryRunDatasetCommand(
            connId,
            null,
            null,
            "SELECT id, total FROM orders",
            null,
            5);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Columns.Should().HaveCount(2);
        result.Columns[0].Name.Should().Be("id");
        result.SampleRows.Should().HaveCount(1);
        result.SampleCount.Should().Be(1);
        result.ExecutionTimeMs.Should().Be(12);
        result.Error.Should().BeNull();

        await _sqlValidator.Received(1).ValidateConnectionTargetAsync("PostgreSQL", "Host=remote;Database=erp", Arg.Any<CancellationToken>());
        _sqlValidator.Received(1).ValidateReadOnlySql("SELECT id, total FROM orders");
    }

    [Fact]
    public async Task ExecuteAsync_WithDirectConnectionString_ValidatesAndExecutesDryRun()
    {
        // Arrange
        var columns = new List<DatasetColumnSchemaDto> { new("val", "integer", false) };
        var sampleRows = new List<IDictionary<string, object?>> { new Dictionary<string, object?> { ["val"] = 1 } };

        _driverStrategy.ExecuteDryRunAsync(
            "Host=adhoc;Database=db",
            "SELECT 1 AS val",
            null,
            5,
            15,
            Arg.Any<CancellationToken>())
            .Returns(new DryRunExecutionResult(columns, sampleRows, 5));

        var command = new DryRunDatasetCommand(
            null,
            "PostgreSQL",
            "Host=adhoc;Database=db",
            "SELECT 1 AS val");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Columns.Should().HaveCount(1);
        await _connectionRepo.DidNotReceive().GetByIdAsync(Arg.Any<DataConnectionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var connId = Guid.NewGuid();
        _connectionRepo.GetByIdAsync(new DataConnectionId(connId), Arg.Any<CancellationToken>())
            .Returns((DataConnection?)null);

        var command = new DryRunDatasetCommand(connId, null, null, "SELECT 1");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderOrConnectionMissing_ThrowsDomainValidationException()
    {
        // Arrange
        var command = new DryRunDatasetCommand(null, "", "", "SELECT 1");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSsrfForbidden_ThrowsForbiddenException()
    {
        // Arrange
        _sqlValidator.ValidateConnectionTargetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ForbiddenException("Loopback blocked"));

        var command = new DryRunDatasetCommand(null, "PostgreSQL", "Host=localhost;Database=db", "SELECT 1");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReadOnlySqlValidationFails_ThrowsDomainValidationException()
    {
        // Arrange
        _sqlValidator.When(v => v.ValidateReadOnlySql("DROP TABLE users"))
            .Do(_ => throw new DomainValidationException("Forbidden SQL token"));

        var command = new DryRunDatasetCommand(null, "PostgreSQL", "Host=remote;Database=db", "DROP TABLE users");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSqlEngineErrorOccurs_ReturnsSuccessFalseWithErrorMessage()
    {
        // Arrange
        _driverStrategy.ExecuteDryRunAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IDictionary<string, object>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("relation \"nonexistent_table\" does not exist"));

        var command = new DryRunDatasetCommand(null, "PostgreSQL", "Host=remote;Database=db", "SELECT * FROM nonexistent_table");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("nonexistent_table");
        result.Columns.Should().BeEmpty();
        result.SampleRows.Should().BeEmpty();
    }
}

