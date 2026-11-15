using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Extensions;
using SmkDoc.Infrastructure.Notifications;
using StackExchange.Redis;

namespace SmkDoc.Tests.Infrastructure.Extensions;

public sealed class DistributedInfrastructureExtensionsTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void AddDistributedInfrastructure_WithoutRedis_RegistersPostgresIdempotencyMemoryCacheAndWebhookClient(string environmentName)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = environmentName
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddDistributedInfrastructure(configuration);

        // Assert
        services.FirstOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer)).Should().BeNull();

        var idempotencyDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IIdempotencyStore));
        idempotencyDesc.Should().NotBeNull();
        idempotencyDesc!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        idempotencyDesc.ImplementationType.Should().BeNull();
        idempotencyDesc.ImplementationFactory.Should().NotBeNull();

        var datasetCacheDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IDatasetResultCache));
        datasetCacheDesc.Should().NotBeNull();
        datasetCacheDesc!.ImplementationType.Should().Be<MemoryDatasetResultCache>();

        var webhookDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IWebhookDispatcher));
        webhookDesc.Should().NotBeNull();
        webhookDesc!.ImplementationType.Should().Be<WebhookDispatcher>();
    }
}
