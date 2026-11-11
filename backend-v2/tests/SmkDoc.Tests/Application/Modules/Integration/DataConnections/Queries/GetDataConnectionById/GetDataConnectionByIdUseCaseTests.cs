using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
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
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    public GetDataConnectionByIdUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(ProjectId.New());
    }

    private GetDataConnectionByIdUseCase CreateSut() => new(_connectionRepo, _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenProjectUnresolved_ThrowsForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetDataConnectionByIdQuery(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        await _connectionRepo.DidNotReceive().GetByIdAsync(Arg.Any<DataConnectionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionFound_ReturnsDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        DataConnection conn = DataConnectionBuilder.Postgres(name: "Production DB", connectionString: "enc_secret", id: id);

        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns(conn);

        // Act
        DataConnectionResultDto result = await CreateSut().ExecuteAsync(new GetDataConnectionByIdQuery(id));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(id);
        result.Name.Should().Be("Production DB");
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _connectionRepo.GetByIdAsync(new DataConnectionId(id), Arg.Any<CancellationToken>())
            .Returns((DataConnection?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetDataConnectionByIdQuery(id));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
