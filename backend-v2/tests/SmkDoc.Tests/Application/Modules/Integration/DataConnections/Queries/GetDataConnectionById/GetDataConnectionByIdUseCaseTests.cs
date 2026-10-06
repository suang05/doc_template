using SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Tests.Common.Factories;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;

public class GetDataConnectionByIdUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private GetDataConnectionByIdUseCase CreateSut() =>
        new(_fixture.ConnectionRepo.Object);

    [Fact]
    public async Task ExecuteAsync_WhenConnectionFound_ReturnsDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var conn = DataConnectionTestFactory.Create(id, "Production DB", DatabaseProvider.PostgreSQL, "enc_secret");

        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conn);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new GetDataConnectionByIdQuery(id));

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
        _fixture.ConnectionRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataConnection?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new GetDataConnectionByIdQuery(id));

        // Assert
        result.Should().BeNull();
    }
}
