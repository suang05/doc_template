using Microsoft.EntityFrameworkCore;
using SmkDoc.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SmkDoc.IntegrationTests.Fixtures;

/// <summary>
/// Testcontainers-backed PostgreSQL fixture for integration tests.
/// Uses <c>SMK_TEST_PG_CONNECTION</c> when supplied; otherwise starts an isolated
/// PostgreSQL 17 container when Docker is enabled/available.
/// </summary>
public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } =
        Environment.GetEnvironmentVariable("SMK_TEST_PG_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=smk_doc_integration_test;Username=postgres;Password=postgres";

    public bool IsContainerBacked { get; private set; }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(ConnectionString);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Testcontainers raises platform-specific socket, pipe, or Docker API exceptions when Docker daemon is unavailable locally.")]
    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMK_TEST_PG_CONNECTION")))
        {
            await MigrateDatabaseAsync(ConnectionString);
            return;
        }

        var requireContainers = string.Equals(
            Environment.GetEnvironmentVariable("SMK_TEST_PG_USE_CONTAINERS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        try
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("smk_doc_integration_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            IsContainerBacked = true;
            await MigrateDatabaseAsync(ConnectionString);
        }
        catch when (!requireContainers)
        {
            // Keep metadata-only / DbContextOptions tests functional when Docker daemon is absent locally.
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }

    private static async Task MigrateDatabaseAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();
    }
}
