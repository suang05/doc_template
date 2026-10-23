using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;

public sealed class GetDataConnectionByIdUseCaseTests
{
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();

    private GetDataConnectionByIdUseCase CreateSut() => new(_connectionRepo);

    [Fact]
    public async Task ExecuteAsync_WhenConnectionFound_ReturnsDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        DataConnection conn = DataConnectionBuilder.Postgres(name: "Production DB", connectionString: "enc_secret", id: id);

        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns(conn);

        // Act
        DataConnectionDto? result = await CreateSut().ExecuteAsync(new GetDataConnectionByIdQuery(id));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Name.Should().Be("Production DB");
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionNotFound_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns((DataConnection?)null);

        // Act
        DataConnectionDto? result = await CreateSut().ExecuteAsync(new GetDataConnectionByIdQuery(id));

        // Assert
        result.Should().BeNull();
    }
}
