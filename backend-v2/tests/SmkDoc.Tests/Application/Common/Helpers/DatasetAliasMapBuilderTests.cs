using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Common.Helpers;

public sealed class DatasetAliasMapBuilderTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IDataProtectionService _dataProtection = Substitute.For<IDataProtectionService>();

    [Fact]
    public async Task BuildAsync_WhenAssignmentsEmpty_ReturnsEmptyMapWithoutCallingRepositories()
    {
        // Arrange
        var assignments = Enumerable.Empty<TemplateDataset>();

        // Act
        var result = await DatasetAliasMapBuilder.BuildAsync(
            assignments,
            _datasetRepo,
            _connectionRepo,
            _dataProtection,
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _datasetRepo.DidNotReceive().GetByIdAsync(Arg.Any<DatasetId>(), Arg.Any<CancellationToken>());
        await _connectionRepo.DidNotReceive().GetByIdAsync(Arg.Any<DataConnectionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_WhenAssignmentsValid_ResolvesConnectionAndDecryptsQuery()
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
            .WithSqlQuery("SELECT * FROM orders")
            .Build();
        var connection = new DataConnectionBuilder()
            .WithId(connectionId)
            .WithProvider(DatabaseProvider.PostgreSQL)
            .WithConnectionString("enc:Host=pg")
            .Build();

        _datasetRepo.GetByIdAsync(new DatasetId(datasetId), Arg.Any<CancellationToken>())
            .Returns(dataset);
        _connectionRepo.GetByIdAsync(new DataConnectionId(connectionId), Arg.Any<CancellationToken>())
            .Returns(connection);
        _dataProtection.Decrypt("enc:Host=pg")
            .Returns("Host=pg;Port=5432");

        // Act
        var result = await DatasetAliasMapBuilder.BuildAsync(
            [td],
            _datasetRepo,
            _connectionRepo,
            _dataProtection,
            CancellationToken.None);

        // Assert
        result.Should().ContainKey("ds1");
        result["ds1"].Provider.Should().Be("PostgreSQL");
        result["ds1"].ConnectionString.Should().Be("Host=pg;Port=5432");
        result["ds1"].SqlQuery.Should().Be("SELECT * FROM orders");
    }

    [Fact]
    public async Task BuildAsync_WhenDatasetNotFound_SkipsGracefully()
    {
        // Arrange
        var datasetId = Guid.NewGuid();
        var td = new TemplateDatasetBuilder()
            .WithDatasetId(datasetId)
            .WithAlias("ds1")
            .WithSortOrder(1)
            .WithTime(TestConstants.BaselineTime)
            .Build();

        _datasetRepo.GetByIdAsync(new DatasetId(datasetId), Arg.Any<CancellationToken>())
            .Returns((Dataset?)null);

        // Act
        var result = await DatasetAliasMapBuilder.BuildAsync(
            [td],
            _datasetRepo,
            _connectionRepo,
            _dataProtection,
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _connectionRepo.DidNotReceive().GetByIdAsync(Arg.Any<DataConnectionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_WhenConnectionNotFound_SkipsGracefully()
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

        _datasetRepo.GetByIdAsync(new DatasetId(datasetId), Arg.Any<CancellationToken>())
            .Returns(dataset);
        _connectionRepo.GetByIdAsync(new DataConnectionId(connectionId), Arg.Any<CancellationToken>())
            .Returns((DataConnection?)null);

        // Act
        var result = await DatasetAliasMapBuilder.BuildAsync(
            [td],
            _datasetRepo,
            _connectionRepo,
            _dataProtection,
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _dataProtection.DidNotReceive().Decrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task BuildAsync_WhenDecryptionThrows_ThrowsDataConnectionDecryptionException()
    {
        // Arrange
        var datasetId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var td = new TemplateDatasetBuilder().WithDatasetId(datasetId).WithAlias("ds1").WithSortOrder(1).Build();
        var dataset = new DatasetBuilder().WithId(datasetId).WithDataConnectionId(connectionId).Build();
        var connection = new DataConnectionBuilder().WithId(connectionId).WithConnectionString("corrupted_secret").Build();

        _datasetRepo.GetByIdAsync(new DatasetId(datasetId), Arg.Any<CancellationToken>())
            .Returns(dataset);
        _connectionRepo.GetByIdAsync(new DataConnectionId(connectionId), Arg.Any<CancellationToken>())
            .Returns(connection);
        _dataProtection.Decrypt("corrupted_secret")
            .Throws(new InvalidOperationException("Decryption error"));

        // Act
        var act = () => DatasetAliasMapBuilder.BuildAsync(
            [td],
            _datasetRepo,
            _connectionRepo,
            _dataProtection,
            CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<DataConnectionDecryptionException>();
        ex.Which.ConnectionId.Should().Be(connectionId);
        ex.Which.ErrorCode.Should().Be("DATA_CONNECTION_DECRYPTION_FAILED");
    }
}
