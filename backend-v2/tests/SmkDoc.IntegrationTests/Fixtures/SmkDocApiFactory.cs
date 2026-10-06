namespace SmkDoc.IntegrationTests.Fixtures;

/// <summary>
/// Architectural factory and HTTP client provider for integration testing against SmkDoc.Api.
/// Discovers base URL from environment (e.g. SMK_TEST_API_URL) or defaults to local service.
/// </summary>
public class SmkDocApiFactory : IDisposable
{
    private readonly HttpClient _client;

    public SmkDocApiFactory()
    {
        var baseUrl = Environment.GetEnvironmentVariable("SMK_TEST_API_URL") ?? "http://localhost:5000";
        _client = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public HttpClient CreateClient() => _client;

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
