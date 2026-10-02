using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Cache;

public class MemoryCompiledTemplateCache : ICompiledTemplateCache
{
    private static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromHours(1);
    private readonly IMemoryCache _cache;
    private readonly IDocumentMetrics? _metrics;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _locks = new();

    public MemoryCompiledTemplateCache(IMemoryCache cache, IDocumentMetrics? metrics = null)
    {
        _cache = cache;
        _metrics = metrics;
    }

    public Func<object, string> GetOrAdd(string key, Func<Func<object, string>> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        string cacheKey = $"compiled_template:{key}";
        if (_cache.TryGetValue(cacheKey, out Func<object, string>? cached) && cached != null)
        {
            _metrics?.RecordCacheRequest(isHit: true);
            return cached;
        }

        object keyLock = _locks.GetOrAdd(cacheKey, _ => new object());
        lock (keyLock)
        {
            return _cache.GetOrCreate(cacheKey, entry =>
            {
                _metrics?.RecordCacheRequest(isHit: false);
                entry.SlidingExpiration = DefaultSlidingExpiration;
                return factory();
            })!;
        }
    }
}
