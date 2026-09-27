using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Infrastructure.Cache;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Cache;

public class MemoryCompiledTemplateCacheTests
{
    [Fact]
    public void GetOrAdd_WhenCacheMiss_InvokesFactoryAndStoresResult()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCompiledTemplateCache(memoryCache);

        int factoryCallCount = 0;
        Func<Func<object, string>> factory = () =>
        {
            factoryCallCount++;
            return obj => $"rendered:{obj}";
        };

        var templateFunc = cache.GetOrAdd("key1", factory);

        factoryCallCount.Should().Be(1);
        templateFunc("hello").Should().Be("rendered:hello");
    }

    [Fact]
    public void GetOrAdd_WhenCacheHit_ReturnsCachedFunctionWithoutReinvokingFactory()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCompiledTemplateCache(memoryCache);

        int factoryCallCount = 0;
        Func<Func<object, string>> factory = () =>
        {
            factoryCallCount++;
            return obj => $"data:{obj}";
        };

        var first = cache.GetOrAdd("same_key", factory);
        var second = cache.GetOrAdd("same_key", factory);

        factoryCallCount.Should().Be(1);
        first.Should().BeSameAs(second);
        second("test").Should().Be("data:test");
    }

    [Fact]
    public void GetOrAdd_WithDifferentKeys_InvokesFactoryPerKey()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCompiledTemplateCache(memoryCache);

        int factoryCallCount = 0;

        cache.GetOrAdd("keyA", () => { factoryCallCount++; return o => "A"; });
        cache.GetOrAdd("keyB", () => { factoryCallCount++; return o => "B"; });

        factoryCallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetOrAdd_UnderConcurrentRequests_IsThreadSafe()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCompiledTemplateCache(memoryCache);

        int factoryCallCount = 0;
        Func<Func<object, string>> factory = () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return o => $"result_{o}";
        };

        var tasks = Enumerable.Range(0, 30).Select(_ => Task.Run(() =>
        {
            var fn = cache.GetOrAdd("concurrent_key", factory);
            fn("item").Should().Be("result_item");
        }));

        await Task.WhenAll(tasks);

        factoryCallCount.Should().BeLessThanOrEqualTo(5);
    }
}
