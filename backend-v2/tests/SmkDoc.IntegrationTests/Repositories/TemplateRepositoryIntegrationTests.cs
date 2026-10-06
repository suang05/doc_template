using Microsoft.EntityFrameworkCore;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Repositories;

[Trait("Category", "Integration")]
public class TemplateRepositoryIntegrationTests : IClassFixture<PostgreSqlContainerFixture>
{
    private readonly PostgreSqlContainerFixture _fixture;

    public TemplateRepositoryIntegrationTests(PostgreSqlContainerFixture fixture)
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
        var repo = new TemplateRepository(dbContext);

        // Assert
        repo.Should().NotBeNull();
    }
}
