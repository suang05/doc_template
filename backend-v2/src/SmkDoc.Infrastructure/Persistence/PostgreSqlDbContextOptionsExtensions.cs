using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace SmkDoc.Infrastructure.Persistence;

public static class PostgreSqlDbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UsePostgreSqlConventions(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return optionsBuilder
            .UseNpgsql(connectionString, npgsqlOptionsAction)
            .UseSnakeCaseNamingConvention();
    }

    public static DbContextOptionsBuilder<TContext> UsePostgreSqlConventions<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)optionsBuilder).UsePostgreSqlConventions(connectionString, npgsqlOptionsAction);
        return optionsBuilder;
    }

    public static DbContextOptionsBuilder UsePostgreSqlConventions(
        this DbContextOptionsBuilder optionsBuilder,
        NpgsqlDataSource dataSource,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(dataSource);

        return optionsBuilder
            .UseNpgsql(dataSource, npgsqlOptionsAction)
            .UseSnakeCaseNamingConvention();
    }

    public static DbContextOptionsBuilder<TContext> UsePostgreSqlConventions<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        NpgsqlDataSource dataSource,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)optionsBuilder).UsePostgreSqlConventions(dataSource, npgsqlOptionsAction);
        return optionsBuilder;
    }
}
