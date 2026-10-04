using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Fixtures;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Queries;

public class ListDatasetsUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private ListDatasetsUseCase CreateSut() =>
        new(_fixture.DatasetRepo.Object, _fixture.ConnectionRepo.Object);

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEnrichedDatasets()
    {
        var connId = Guid.NewGuid();
        var conn = new DataConnection("PostgresDB", "PostgreSQL", "enc_pg", id: connId);
        var dataset = new Dataset("CustomerData", "Customer queries", connId, "SELECT * FROM customers", 120);

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
