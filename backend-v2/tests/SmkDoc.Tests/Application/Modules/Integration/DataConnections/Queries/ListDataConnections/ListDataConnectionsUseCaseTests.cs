using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;

public sealed class ListDataConnectionsUseCaseTests
{
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    public ListDataConnectionsUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(ProjectId.New());
    }

    private ListDataConnectionsUseCase CreateSut() => new(_connectionRepo, _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenProjectUnresolved_ThrowsForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);

        // Act
        var act = () => CreateSut().ExecuteAsync(new ListDataConnectionsQuery());

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        await _connectionRepo.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionsExist_ReturnsProjectedDtosWithoutSecrets()
    {
        // Arrange
        DataConnection conn1 = DataConnectionBuilder.Postgres(name: "Postgres Main", connectionString: "enc_secret_1");
        DataConnection conn2 = DataConnectionBuilder.SqlServer(name: "SqlServer Legacy", connectionString: "enc_secret_2");

        _connectionRepo.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DataConnection> { conn1, conn2 });

        // Act
        IReadOnlyList<DataConnectionResultDto> results = await CreateSut().ExecuteAsync(new ListDataConnectionsQuery());

        // Assert
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Postgres Main");
        results[0].Provider.Should().Be("PostgreSQL");
        results[1].Name.Should().Be("SqlServer Legacy");
    }
}
