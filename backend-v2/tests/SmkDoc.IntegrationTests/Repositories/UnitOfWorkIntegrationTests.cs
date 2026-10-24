using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.IntegrationTests.Fixtures;
using Xunit;

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
    public void Constructor_WithDbContextAndDispatcher_InstantiatesSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        using var dbContext = new AppDbContext(options);
        var dispatcher = Substitute.For<IDomainEventDispatcher>();

        // Act
        var uow = new UnitOfWork(dbContext, dispatcher);

        // Assert
        uow.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullDispatcher_InstantiatesSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        using var dbContext = new AppDbContext(options);

        // Act
        var uow = new UnitOfWork(dbContext, null);

        // Assert
        uow.Should().NotBeNull();
    }
}
