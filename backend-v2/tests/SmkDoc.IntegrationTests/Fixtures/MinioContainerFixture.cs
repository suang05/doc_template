namespace SmkDoc.IntegrationTests.Fixtures;

/// <summary>
/// Architectural fixture stub and configuration provider for MinIO/S3 object storage testing.
/// Provides MinIO endpoint discovery and lifecycle hooks for real object storage integration runs.
/// </summary>
public class MinioContainerFixture : IAsyncLifetime
{
    public string Endpoint { get; private set; } =
        Environment.GetEnvironmentVariable("SMK_TEST_MINIO_ENDPOINT") ?? "localhost:9000";

    public string AccessKey { get; private set; } =
        Environment.GetEnvironmentVariable("SMK_TEST_MINIO_ACCESS_KEY") ?? "minioadmin";

    public string SecretKey { get; private set; } =
        Environment.GetEnvironmentVariable("SMK_TEST_MINIO_SECRET_KEY") ?? "minioadmin";

    public bool UseSsl => false;

    public Task InitializeAsync()
    {
        // Extension point: Initialize MinIO Testcontainer or verify local endpoint availability
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
