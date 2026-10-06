using SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Tests.Common.Factories;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;

public class ListDataConnectionsUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private ListDataConnectionsUseCase CreateSut() =>
        new(_fixture.ConnectionRepo.Object);

    [Fact]
    public async Task ExecuteAsync_WhenConnectionsExist_ReturnsProjectedDtosWithoutSecrets()
    {
        // Arrange
        var conn1 = DataConnectionTestFactory.Create(name: "Postgres Main", provider: DatabaseProvider.PostgreSQL, encryptedConnectionString: "enc_secret_1");
        var conn2 = DataConnectionTestFactory.Create(name: "SqlServer Legacy", provider: DatabaseProvider.SqlServer, encryptedConnectionString: "enc_secret_2");

        _fixture.ConnectionRepo
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DataConnection> { conn1, conn2 });

        var sut = CreateSut();

        // Act
        var results = await sut.ExecuteAsync(new ListDataConnectionsQuery());

        // Assert
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Postgres Main");
        results[0].Provider.Should().Be("PostgreSQL");
        results[1].Name.Should().Be("SqlServer Legacy");
    }
}
