using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Queries.ListDatasets;

public sealed class ListDatasetsUseCaseTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();

    private ListDatasetsUseCase CreateSut() => new(
        _datasetRepo,
        _connectionRepo);

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEnrichedDatasets()
    {
        // Arrange
        var connId = Guid.NewGuid();
        DataConnection conn = DataConnectionBuilder.Postgres(name: "PostgresDB", connectionString: "enc_pg", id: connId);
        Dataset dataset = new DatasetBuilder()
            .WithName("CustomerData")
            .WithDescription("Customer queries")
            .WithDataConnectionId(connId)
            .WithSqlQuery("SELECT * FROM customers")
            .WithCacheSeconds(120);

        _datasetRepo.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Dataset> { dataset });
        _connectionRepo.GetByIdsAsync(Arg.Any<IEnumerable<DataConnectionId>>(), Arg.Any<CancellationToken>())
            .Returns(new List<DataConnection> { conn });

        // Act
        IReadOnlyList<DatasetDto> results = await CreateSut().ExecuteAsync(new ListDatasetsQuery());

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("CustomerData");
        results[0].DataConnectionName.Should().Be("PostgresDB");
        results[0].CacheSeconds.Should().Be(120);
    }
}
