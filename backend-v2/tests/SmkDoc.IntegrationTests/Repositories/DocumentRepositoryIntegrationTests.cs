using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Repositories;

[Trait("Category", "Integration")]
public class DocumentRepositoryIntegrationTests : IClassFixture<PostgreSqlContainerFixture>
{
    private readonly PostgreSqlContainerFixture _fixture;

    public DocumentRepositoryIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Constructor_WhenConfiguredWithNpgsqlOptions_InstantiatesSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        using var dbContext = new AppDbContext(options);

        // Act
        var repo = new EfRepository<Document>(dbContext);

        // Assert
        repo.Should().NotBeNull();
    }
}
