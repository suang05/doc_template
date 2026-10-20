using SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Queries.GetDatasetById;

public class GetDatasetByIdUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private GetDatasetByIdUseCase CreateSut() => _fixture.BuildGetDatasetByIdUseCase();

    [Fact]
    public async Task ExecuteAsync_WhenFound_ReturnsEnrichedDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var connId = Guid.NewGuid();
        var conn = DataConnectionBuilder.Postgres(name: "AnalyticsDB", connectionString: "enc_analytics", id: connId);
        var dataset = new DatasetBuilder().WithId(id).WithName("SalesMetrics").WithDescription("desc").WithDataConnectionId(connId).WithSqlQuery("SELECT * FROM sales").WithCacheSeconds(300).Build();

        _fixture.DatasetRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataset);

        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(connId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conn);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetDatasetByIdQuery(id));

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
        _fixture.DatasetRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dataset?)null);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetDatasetByIdQuery(id));

        // Assert
        result.Should().BeNull();
    }
}
