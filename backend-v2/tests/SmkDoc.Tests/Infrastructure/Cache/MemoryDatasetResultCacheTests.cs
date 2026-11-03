using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Infrastructure.Cache;

namespace SmkDoc.Tests.Infrastructure.Cache;

public sealed class MemoryDatasetResultCacheTests
{
    [Fact]
    public async Task GetAsync_WhenEntryDoesNotExist_ReturnsNull()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var sut = new MemoryDatasetResultCache(memoryCache);

        var json = await sut.GetAsync("missing-key");

        json.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WhenValidPayloadAndTtl_StoresAndReturnsCachedJson()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var sut = new MemoryDatasetResultCache(memoryCache);

        await sut.SetAsync("tenant:ds:hash", "[{\"id\":1}]", TimeSpan.FromSeconds(30));
        var json = await sut.GetAsync("tenant:ds:hash");

        json.Should().Be("[{\"id\":1}]");
    }

    [Fact]
    public async Task SetAsync_WhenTtlExpires_EvictsCachedEntry()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var sut = new MemoryDatasetResultCache(memoryCache);

        await sut.SetAsync("ephemeral-key", "[{\"id\":2}]", TimeSpan.FromMilliseconds(40));
        await Task.Delay(90);

        var json = await sut.GetAsync("ephemeral-key");

        json.Should().BeNull();
    }

    [Theory]
    [InlineData("", 60)]
    [InlineData("[{\"id\":1}]", 0)]
    [InlineData("[{\"id\":1}]", -5)]
    public async Task SetAsync_WhenPayloadEmptyOrTtlNonPositive_DoesNotCache(string payload, int ttlSeconds)
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var sut = new MemoryDatasetResultCache(memoryCache);

        await sut.SetAsync("bypass-key", payload, TimeSpan.FromSeconds(ttlSeconds));

        (await sut.GetAsync("bypass-key")).Should().BeNull();
    }
}
