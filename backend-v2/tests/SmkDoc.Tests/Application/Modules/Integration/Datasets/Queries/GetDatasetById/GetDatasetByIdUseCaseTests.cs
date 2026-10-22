using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Queries.GetDatasetById;

public sealed class GetDatasetByIdUseCaseTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();

    private GetDatasetByIdUseCase CreateSut() => new(
        _datasetRepo,
        _connectionRepo);

    [Fact]
    public async Task ExecuteAsync_WhenFound_ReturnsEnrichedDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var connId = Guid.NewGuid();
        DataConnection conn = DataConnectionBuilder.Postgres(name: "AnalyticsDB", connectionString: "enc_analytics", id: connId);
        Dataset dataset = new DatasetBuilder()
            .WithId(id)
            .WithName("SalesMetrics")
            .WithDescription("desc")
            .WithDataConnectionId(connId)
            .WithSqlQuery("SELECT * FROM sales")
            .WithCacheSeconds(300);

        _datasetRepo.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(dataset);

        _connectionRepo.GetByIdAsync(connId, Arg.Any<CancellationToken>())
            .Returns(conn);

        // Act
        DatasetDto? result = await CreateSut().ExecuteAsync(new GetDatasetByIdQuery(id));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Name.Should().Be("SalesMetrics");
        result.DataConnectionName.Should().Be("AnalyticsDB");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _datasetRepo.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((Dataset?)null);

        // Act
        DatasetDto? result = await CreateSut().ExecuteAsync(new GetDatasetByIdQuery(id));

        // Assert
        result.Should().BeNull();
    }
}
