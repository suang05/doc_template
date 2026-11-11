using System.Reflection;
using NSubstitute.ExceptionExtensions;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Services;

public sealed class DatasetResolverTests
{
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly ISqlConnectionValidator _sqlValidator = Substitute.For<ISqlConnectionValidator>();
    private readonly IDataProtectionService _dataProtection = Substitute.For<IDataProtectionService>();

    private DatasetResolver CreateSut() => new(
        _connectionRepo,
        _datasetRepo,
        _sqlValidator,
        _dataProtection);

    [Fact]
    public async Task ValidateAndResolveConnectionAsync_WhenValid_ReturnsDataConnection()
    {
        // Arrange
        var connectionId = Guid.NewGuid();
        var connection = new DataConnectionBuilder().WithId(connectionId).Build();
        _connectionRepo.GetByIdAsync(new DataConnectionId(connectionId), Arg.Any<CancellationToken>())
            .Returns(connection);

        // Act
        var result = await CreateSut().ValidateAndResolveConnectionAsync(
            new DataConnectionId(connectionId),
            "SELECT 1",
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(connection);
        _sqlValidator.Received(1).ValidateReadOnlySql("SELECT 1");
        await _connectionRepo.Received(1).GetByIdAsync(new DataConnectionId(connectionId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAndResolveConnectionAsync_WhenConnectionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var connectionId = Guid.NewGuid();
        _connectionRepo.GetByIdAsync(new DataConnectionId(connectionId), Arg.Any<CancellationToken>())
            .Returns((DataConnection?)null);

        // Act
        var act = () => CreateSut().ValidateAndResolveConnectionAsync(
            new DataConnectionId(connectionId),
            "SELECT 1",
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _sqlValidator.Received(1).ValidateReadOnlySql("SELECT 1");
    }

    [Fact]
    public async Task ValidateAndResolveConnectionAsync_WhenSqlValidatorThrows_BubblesException()
    {
        // Arrange
        var connectionId = Guid.NewGuid();
        _sqlValidator.When(v => v.ValidateReadOnlySql("DROP TABLE users"))
            .Throw(new DomainValidationException("Mutating SQL is not allowed."));

        // Act
        var act = () => CreateSut().ValidateAndResolveConnectionAsync(
            new DataConnectionId(connectionId),
            "DROP TABLE users",
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*Mutating SQL is not allowed*");
        await _connectionRepo.DidNotReceive().GetByIdAsync(Arg.Any<DataConnectionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAssignmentsAsync_WhenAssignmentsEmpty_ReturnsEmptyMapWithoutCallingRepositories()
    {
        // Arrange
        var assignments = Enumerable.Empty<TemplateDataset>();

        // Act
        var result = await CreateSut().ResolveAssignmentsAsync(assignments, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _datasetRepo.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<DatasetId>>(), Arg.Any<CancellationToken>());
        await _connectionRepo.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<DataConnectionId>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAssignmentsAsync_WhenDatasetNavigationHydrated_UsesSingleBatchQueryWithoutCallingConnectionRepository()
    {
        // Arrange
        var datasetId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var td = new TemplateDatasetBuilder()
            .WithDatasetId(datasetId)
            .WithAlias("ds1")
            .WithSortOrder(1)
            .WithTime(TestConstants.BaselineTime)
            .Build();
        var connection = new DataConnectionBuilder()
            .WithId(connectionId)
            .WithProvider(DatabaseProvider.PostgreSQL)
            .WithConnectionString("enc:Host=pg")
            .Build();
        var dataset = new DatasetBuilder()
            .WithId(datasetId)
            .WithDataConnectionId(connectionId)
            .WithSqlQuery("SELECT * FROM orders")
            .WithCacheSeconds(120)
            .Build();

        typeof(Dataset)
            .GetProperty(nameof(Dataset.DataConnection), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(dataset, connection);

        _datasetRepo.GetByIdsAsync(Arg.Any<IEnumerable<DatasetId>>(), Arg.Any<CancellationToken>())
            .Returns([dataset]);
        _dataProtection.Decrypt("enc:Host=pg")
            .Returns("Host=pg;Port=5432");

        // Act
        var result = await CreateSut().ResolveAssignmentsAsync([td], CancellationToken.None);

        // Assert
        result.Should().ContainKey("ds1");
        result["ds1"].Provider.Should().Be("PostgreSQL");
        result["ds1"].ConnectionString.Should().Be("Host=pg;Port=5432");
        result["ds1"].SqlQuery.Should().Be("SELECT * FROM orders");
        result["ds1"].DatasetId.Should().Be(datasetId);
        result["ds1"].DataConnectionId.Should().Be(connectionId);
        result["ds1"].CacheSeconds.Should().Be(120);

        await _datasetRepo.Received(1).GetByIdsAsync(Arg.Any<IEnumerable<DatasetId>>(), Arg.Any<CancellationToken>());
        await _connectionRepo.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<DataConnectionId>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAssignmentsAsync_WhenMultipleAliasesShareDataConnection_BatchesAndDecryptsConnectionOnce()
    {
        // Arrange
        var datasetId1 = Guid.NewGuid();
        var datasetId2 = Guid.NewGuid();
        var sharedConnectionId = Guid.NewGuid();

        var td1 = new TemplateDatasetBuilder().WithDatasetId(datasetId1).WithAlias("orders").WithSortOrder(1).Build();
        var td2 = new TemplateDatasetBuilder().WithDatasetId(datasetId2).WithAlias("items").WithSortOrder(2).Build();

        var ds1 = new DatasetBuilder().WithId(datasetId1).WithDataConnectionId(sharedConnectionId).WithSqlQuery("SELECT 1").WithCacheSeconds(30).Build();
        var ds2 = new DatasetBuilder().WithId(datasetId2).WithDataConnectionId(sharedConnectionId).WithSqlQuery("SELECT 2").WithCacheSeconds(60).Build();
        var sharedConnection = new DataConnectionBuilder()
            .WithId(sharedConnectionId)
            .WithProvider(DatabaseProvider.PostgreSQL)
            .WithConnectionString("enc:shared")
            .Build();

        _datasetRepo.GetByIdsAsync(Arg.Any<IEnumerable<DatasetId>>(), Arg.Any<CancellationToken>())
            .Returns([ds1, ds2]);
        _connectionRepo.GetByIdsAsync(Arg.Any<IEnumerable<DataConnectionId>>(), Arg.Any<CancellationToken>())
            .Returns([sharedConnection]);
        _dataProtection.Decrypt("enc:shared")
            .Returns("Host=shared-db");

        // Act
        var result = await CreateSut().ResolveAssignmentsAsync([td1, td2], CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result["orders"].CacheSeconds.Should().Be(30);
        result["items"].CacheSeconds.Should().Be(60);
        _dataProtection.Received(1).Decrypt("enc:shared");
    }

    [Fact]
    public async Task ResolveAssignmentsAsync_WhenDatasetNotFound_SkipsGracefully()
    {
        // Arrange
        var datasetId = Guid.NewGuid();
        var td = new TemplateDatasetBuilder()
            .WithDatasetId(datasetId)
            .WithAlias("ds1")
            .WithSortOrder(1)
            .WithTime(TestConstants.BaselineTime)
            .Build();

        _datasetRepo.GetByIdsAsync(Arg.Any<IEnumerable<DatasetId>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Dataset>());

        // Act
        var result = await CreateSut().ResolveAssignmentsAsync([td], CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _connectionRepo.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<DataConnectionId>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAssignmentsAsync_WhenConnectionNotFound_SkipsGracefully()
    {
        // Arrange
        var datasetId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var td = new TemplateDatasetBuilder()
            .WithDatasetId(datasetId)
            .WithAlias("ds1")
            .WithSortOrder(1)
            .WithTime(TestConstants.BaselineTime)
            .Build();
        var dataset = new DatasetBuilder()
            .WithId(datasetId)
            .WithDataConnectionId(connectionId)
            .Build();

        _datasetRepo.GetByIdsAsync(Arg.Any<IEnumerable<DatasetId>>(), Arg.Any<CancellationToken>())
            .Returns([dataset]);
        _connectionRepo.GetByIdsAsync(Arg.Any<IEnumerable<DataConnectionId>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DataConnection>());

        // Act
        var result = await CreateSut().ResolveAssignmentsAsync([td], CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _dataProtection.DidNotReceive().Decrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveAssignmentsAsync_WhenDecryptionThrows_ThrowsDataConnectionDecryptionException()
    {
        // Arrange
        var datasetId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var td = new TemplateDatasetBuilder().WithDatasetId(datasetId).WithAlias("ds1").WithSortOrder(1).Build();
        var dataset = new DatasetBuilder().WithId(datasetId).WithDataConnectionId(connectionId).Build();
        var connection = new DataConnectionBuilder().WithId(connectionId).WithConnectionString("corrupted_secret").Build();

        _datasetRepo.GetByIdsAsync(Arg.Any<IEnumerable<DatasetId>>(), Arg.Any<CancellationToken>())
            .Returns([dataset]);
        _connectionRepo.GetByIdsAsync(Arg.Any<IEnumerable<DataConnectionId>>(), Arg.Any<CancellationToken>())
            .Returns([connection]);
        _dataProtection.Decrypt("corrupted_secret")
            .Throws(new InvalidOperationException("Decryption error"));

        // Act
        var act = () => CreateSut().ResolveAssignmentsAsync([td], CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<DataConnectionDecryptionException>();
        ex.Which.ConnectionId.Should().Be(connectionId);
        ex.Which.ErrorCode.Should().Be("DATA_CONNECTION_DECRYPTION_FAILED");
    }
}

