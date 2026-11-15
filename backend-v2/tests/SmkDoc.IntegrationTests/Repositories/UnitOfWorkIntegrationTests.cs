using Microsoft.EntityFrameworkCore;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Repositories;

[Trait("Category", "Integration")]
public sealed class UnitOfWorkIntegrationTests : IClassFixture<PostgreSqlContainerFixture>
{
    private readonly PostgreSqlContainerFixture _fixture;

    public UnitOfWorkIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Constructor_WithDbContext_InstantiatesSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        using var dbContext = new AppDbContext(options);

        // Act
        var uow = new UnitOfWork(dbContext);

        // Assert
        uow.Should().NotBeNull();
    }
}
