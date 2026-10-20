using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Common.Helpers;

public class DatasetAliasMapBuilderTests
{
    private readonly Mock<IDatasetRepository> _datasetRepoMock = new();
    private readonly Mock<IDataConnectionRepository> _connectionRepoMock = new();
    private readonly Mock<IDataProtectionService> _dataProtectionMock = new();

    [Fact]
    public async Task BuildAsync_WhenAssignmentsEmpty_ReturnsEmptyMapWithoutCallingRepositories()
    {
        // Arrange
        var assignments = Enumerable.Empty<TemplateDataset>();

        // Act
        var result = await DatasetAliasMapBuilder.BuildAsync(
            assignments,
            _datasetRepoMock.Object,
            _connectionRepoMock.Object,
            _dataProtectionMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _datasetRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _connectionRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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

        _datasetRepoMock.Setup(r => r.GetByIdAsync(datasetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataset);
        _connectionRepoMock.Setup(r => r.GetByIdAsync(connectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _dataProtectionMock.Setup(p => p.Decrypt("enc:Host=pg"))
            .Returns("Host=pg;Port=5432");

        // Act
        var result = await DatasetAliasMapBuilder.BuildAsync(
            new[] { td },
            _datasetRepoMock.Object,
            _connectionRepoMock.Object,
            _dataProtectionMock.Object,
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

        _datasetRepoMock.Setup(r => r.GetByIdAsync(datasetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dataset?)null);

        // Act
        var result = await DatasetAliasMapBuilder.BuildAsync(
            new[] { td },
            _datasetRepoMock.Object,
            _connectionRepoMock.Object,
            _dataProtectionMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _connectionRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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

        _datasetRepoMock.Setup(r => r.GetByIdAsync(datasetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataset);
        _connectionRepoMock.Setup(r => r.GetByIdAsync(connectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataConnection?)null);

        // Act
        var result = await DatasetAliasMapBuilder.BuildAsync(
            new[] { td },
            _datasetRepoMock.Object,
            _connectionRepoMock.Object,
            _dataProtectionMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _dataProtectionMock.Verify(p => p.Decrypt(It.IsAny<string>()), Times.Never);
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

        _datasetRepoMock.Setup(r => r.GetByIdAsync(datasetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataset);
        _connectionRepoMock.Setup(r => r.GetByIdAsync(connectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _dataProtectionMock.Setup(p => p.Decrypt("corrupted_secret"))
            .Throws(new InvalidOperationException("Decryption error"));

        // Act
        var act = () => DatasetAliasMapBuilder.BuildAsync(
            new[] { td },
            _datasetRepoMock.Object,
            _connectionRepoMock.Object,
            _dataProtectionMock.Object,
            CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<DataConnectionDecryptionException>();
        ex.Which.ConnectionId.Should().Be(connectionId);
        ex.Which.ErrorCode.Should().Be("DATA_CONNECTION_DECRYPTION_FAILED");
    }
}
