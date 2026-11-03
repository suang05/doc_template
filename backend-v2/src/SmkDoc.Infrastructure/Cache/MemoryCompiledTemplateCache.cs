using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Cache;

public sealed class MemoryCompiledTemplateCache : ICompiledTemplateCache, IDisposable
{
    private const int StripeCount = 64;
    private static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromHours(1);

    private readonly IMemoryCache _cache;
    private readonly IDocumentMetrics? _metrics;
    private readonly Lock[] _stripes;
    private readonly Lock _tokenLock = new();
    private CancellationTokenSource _resetCts = new();
    private bool _disposed;

    public MemoryCompiledTemplateCache(IMemoryCache cache, IDocumentMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        _metrics = metrics;
        _stripes = new Lock[StripeCount];
        for (int i = 0; i < StripeCount; i++)
        {
            _stripes[i] = new Lock();
        }
    }

    public Func<object, string> GetOrAdd(string key, Func<Func<object, string>> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        string cacheKey = $"compiled_template:{key}";
        if (_cache.TryGetValue(cacheKey, out Func<object, string>? cached) && cached is not null)
        {
            _metrics?.RecordCacheRequest(isHit: true);
            return cached;
        }

        var stripe = _stripes[(uint)cacheKey.GetHashCode() % StripeCount];
        lock (stripe)
        {
            if (_cache.TryGetValue(cacheKey, out cached) && cached is not null)
            {
                _metrics?.RecordCacheRequest(isHit: true);
                return cached;
            }

            _metrics?.RecordCacheRequest(isHit: false);
            var compiled = factory();

            CancellationChangeToken expirationToken;
            lock (_tokenLock)
            {
                expirationToken = new CancellationChangeToken(_resetCts.Token);
            }

            var entryOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = DefaultSlidingExpiration
            };
            entryOptions.AddExpirationToken(expirationToken);

            _cache.Set(cacheKey, compiled, entryOptions);
            return compiled;
        }
    }

    public void InvalidateAll()
    {
        CancellationTokenSource previous;
        lock (_tokenLock)
        {
            if (_disposed)
            {
                return;
            }

            previous = _resetCts;
            _resetCts = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    public void Dispose()
    {
        CancellationTokenSource? toDispose = null;
        lock (_tokenLock)
        {
            if (!_disposed)
            {
                _disposed = true;
                toDispose = _resetCts;
            }
        }

        toDispose?.Dispose();
    }
}
