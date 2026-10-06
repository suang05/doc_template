namespace SmkDoc.IntegrationTests.Fixtures;

/// <summary>
/// Architectural fixture stub and configuration provider for PostgreSQL containerized testing.
/// Provides connection string discovery and lifecycle hooks for real database integration runs.
/// </summary>
public class PostgreSqlContainerFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } =
        Environment.GetEnvironmentVariable("SMK_TEST_PG_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=smk_doc_integration_test;Username=postgres;Password=postgres";

    public bool IsAvailable => !string.IsNullOrWhiteSpace(ConnectionString);

    public Task InitializeAsync()
    {
        // Extension point: Initialize Testcontainers instance or verify local Docker container availability
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Extension point: Stop and dispose Testcontainers instance if dynamically spawned
        return Task.CompletedTask;
    }
}
