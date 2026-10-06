using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmkDoc.Infrastructure.Storage;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Storage;

[Trait("Category", "Integration")]
public class MinioStorageServiceIntegrationTests : IClassFixture<MinioContainerFixture>
{
    private readonly MinioContainerFixture _fixture;

    public MinioStorageServiceIntegrationTests(MinioContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Constructor_WhenConfiguredWithSettings_InitializesClientSuccessfully()
    {
        // Arrange
        var settings = Options.Create(new MinioSettings
        {
            Endpoint = _fixture.Endpoint,
            AccessKey = _fixture.AccessKey,
            SecretKey = _fixture.SecretKey,
            Secure = _fixture.UseSsl,
            PublicEndpoint = $"http://{_fixture.Endpoint}"
        });

        // Act
        var service = new MinioStorageService(settings, NullLogger<MinioStorageService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }
}
