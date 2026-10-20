using SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Queries.ListDatasets;

public class ListDatasetsUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private ListDatasetsUseCase CreateSut() => _fixture.BuildListDatasetsUseCase();

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEnrichedDatasets()
    {
        var connId = Guid.NewGuid();
        var conn = DataConnectionBuilder.Postgres(name: "PostgresDB", connectionString: "enc_pg", id: connId);
        var dataset = new DatasetBuilder().WithName("CustomerData").WithDescription("Customer queries").WithDataConnectionId(connId).WithSqlQuery("SELECT * FROM customers").WithCacheSeconds(120).Build();

        _fixture.DatasetRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dataset> { dataset });
        _fixture.ConnectionRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DataConnection> { conn });

        var results = await CreateSut().ExecuteAsync(new ListDatasetsQuery());

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("CustomerData");
        results[0].DataConnectionName.Should().Be("PostgresDB");
        results[0].CacheSeconds.Should().Be(120);
    }
}
