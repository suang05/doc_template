using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Infrastructure.Data.Drivers;

namespace SmkDoc.Tests.Infrastructure.Data;

public sealed class SqlDriverFactoryTests
{
    private readonly ISqlDriverStrategy _postgresStrategy = Substitute.For<ISqlDriverStrategy>();
    private readonly ISqlDriverStrategy _sqlServerStrategy = Substitute.For<ISqlDriverStrategy>();

    public SqlDriverFactoryTests()
    {
        _postgresStrategy.ProviderName.Returns("PostgreSQL");
        _sqlServerStrategy.ProviderName.Returns("SqlServer");
    }

    [Fact]
    public void GetStrategy_WhenProviderExists_ShouldReturnMatchingStrategyCaseInsensitively()
    {
        // Arrange
        var factory = new SqlDriverFactory([_postgresStrategy, _sqlServerStrategy]);

        // Act
        var strategyLower = factory.GetStrategy("postgresql");
        var strategyUpper = factory.GetStrategy("POSTGRESQL");
        var strategyMixed = factory.GetStrategy("PostgreSql");
        var strategySqlServer = factory.GetStrategy("sqlserver");

        // Assert
        strategyLower.Should().BeSameAs(_postgresStrategy);
        strategyUpper.Should().BeSameAs(_postgresStrategy);
        strategyMixed.Should().BeSameAs(_postgresStrategy);
        strategySqlServer.Should().BeSameAs(_sqlServerStrategy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetStrategy_WhenProviderNullOrWhitespace_ShouldThrowNotSupportedException(string? provider)
    {
        // Arrange
        var factory = new SqlDriverFactory([_postgresStrategy]);

        // Act
        var act = () => factory.GetStrategy(provider!);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*must be specified*");
    }

    [Fact]
    public void GetStrategy_WhenProviderNotRegistered_ShouldThrowNotSupportedException()
    {
        // Arrange
        var factory = new SqlDriverFactory([_postgresStrategy]);

        // Act
        var act = () => factory.GetStrategy("Oracle");

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Data connection provider 'Oracle' is not currently supported.*");
    }
}

