using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;

public sealed class ListDataConnectionsUseCaseTests
{
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();

    private ListDataConnectionsUseCase CreateSut() => new(_connectionRepo);

    [Fact]
    public async Task ExecuteAsync_WhenConnectionsExist_ReturnsProjectedDtosWithoutSecrets()
    {
        // Arrange
        DataConnection conn1 = DataConnectionBuilder.Postgres(name: "Postgres Main", connectionString: "enc_secret_1");
        DataConnection conn2 = DataConnectionBuilder.SqlServer(name: "SqlServer Legacy", connectionString: "enc_secret_2");

        _connectionRepo.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DataConnection> { conn1, conn2 });

        // Act
        IReadOnlyList<DataConnectionDto> results = await CreateSut().ExecuteAsync(new ListDataConnectionsQuery());

        // Assert
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Postgres Main");
        results[0].Provider.Should().Be("PostgreSQL");
        results[1].Name.Should().Be("SqlServer Legacy");
    }
}
