using SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Tests.Common.Factories;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Queries.GetDatasetById;

public class GetDatasetByIdUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private GetDatasetByIdUseCase CreateSut() =>
        new(_fixture.DatasetRepo.Object, _fixture.ConnectionRepo.Object);

    [Fact]
    public async Task ExecuteAsync_WhenFound_ReturnsEnrichedDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var connId = Guid.NewGuid();
        var conn = DataConnectionTestFactory.Create(connId, "AnalyticsDB", DatabaseProvider.PostgreSQL, "enc_analytics");
        var dataset = DatasetTestFactory.Create(id, "SalesMetrics", "desc", connId, "SELECT * FROM sales", 300);

        _fixture.DatasetRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataset);

        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(connId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conn);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new GetDatasetByIdQuery(id));

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

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new GetDatasetByIdQuery(id));

        // Assert
        result.Should().BeNull();
    }
}
