using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Extensions;
using SmkDoc.Infrastructure.Queue;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Extensions;

public sealed class DistributedInfrastructureExtensionsTests
{
    [Fact]
    public void AddDistributedInfrastructure_InProductionWithoutRedis_ThrowsInvalidOperationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act
        var act = () => services.AddDistributedInfrastructure(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*strictly required in Production environment*");
    }

    [Fact]
    public void AddDistributedInfrastructure_WhenEnforceDistributedFlagTrueWithoutRedis_ThrowsInvalidOperationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["EnforceDistributedInfrastructure"] = "true"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act
        var act = () => services.AddDistributedInfrastructure(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*strictly required in Production environment*");
    }

    [Fact]
    public void AddDistributedInfrastructure_InDevelopmentWithoutRedis_RegistersMemoryFallbacks()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddDistributedInfrastructure(configuration);

        // Assert
        var renderQueueDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IDocumentRenderQueue));
        renderQueueDesc.Should().NotBeNull();
        renderQueueDesc!.ImplementationType.Should().Be(typeof(MemoryDocumentRenderQueue));

        var idempotencyDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IIdempotencyStore));
        idempotencyDesc.Should().NotBeNull();
        idempotencyDesc!.ImplementationType.Should().Be(typeof(MemoryIdempotencyStore));

        var datasetCacheDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IDatasetResultCache));
        datasetCacheDesc.Should().NotBeNull();
        datasetCacheDesc!.ImplementationType.Should().Be(typeof(MemoryDatasetResultCache));
    }
}

