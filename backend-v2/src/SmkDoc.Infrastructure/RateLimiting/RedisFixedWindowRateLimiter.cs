using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using SmkDoc.Infrastructure.Cache;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.RateLimiting;

/// <summary>
/// Cluster-wide fixed-window <see cref="RateLimiter"/> backed by an atomic Redis Lua counter.
/// Fails open transparently to an in-process <see cref="FixedWindowRateLimiter"/> if Redis is unreachable.
/// </summary>
public sealed partial class RedisFixedWindowRateLimiter : RateLimiter
{
    internal const string FixedWindowLuaScript = """
        local current = redis.call('INCRBY', KEYS[1], ARGV[1])
        if current == tonumber(ARGV[1]) then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        local ttl = redis.call('PTTL', KEYS[1])
        if ttl < 0 then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
            ttl = tonumber(ARGV[2])
        end
        return { current, ttl }
        """;

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly string _partitionKey;
    private readonly FixedWindowRateLimiterOptions _options;
    private readonly ILogger _logger;
    private readonly RedisKey _redisKey;
    private readonly long _windowMs;
    private readonly FixedWindowRateLimiter _fallbackLimiter;
    private long _lastActivityTimestamp = Stopwatch.GetTimestamp();

    public RedisFixedWindowRateLimiter(
        IConnectionMultiplexer multiplexer,
        string partitionKey,
        FixedWindowRateLimiterOptions options,
        ILogger logger,
        string instancePrefix = RedisConfigurationResolver.DefaultInstancePrefix)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _multiplexer = multiplexer;
        _partitionKey = partitionKey;
        _options = options;
        _logger = logger;
        _redisKey = $"{instancePrefix}ratelimit:api:{partitionKey}";
        _windowMs = Math.Max(1L, (long)Math.Ceiling(options.Window.TotalMilliseconds));
        _fallbackLimiter = new FixedWindowRateLimiter(options);
    }

    public override TimeSpan? IdleDuration =>
        Stopwatch.GetElapsedTime(Volatile.Read(ref _lastActivityTimestamp));

    public override RateLimiterStatistics? GetStatistics() =>
        _fallbackLimiter.GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        Volatile.Write(ref _lastActivityTimestamp, Stopwatch.GetTimestamp());
        return _fallbackLimiter.AttemptAcquire(permitCount);
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref _lastActivityTimestamp, Stopwatch.GetTimestamp());

        if (permitCount == 0)
        {
            return new DistributedRateLimitLease(isAcquired: true, retryAfter: null);
        }

        try
        {
            var result = await _multiplexer.GetDatabase().ScriptEvaluateAsync(
                FixedWindowLuaScript,
                [_redisKey],
                [permitCount, _windowMs]);

            var values = (RedisResult[]?)result;
            if (values is { Length: >= 2 })
            {
                long currentCount = (long)values[0];
                long remainingTtlMs = Math.Max(1L, (long)values[1]);

                if (currentCount <= _options.PermitLimit)
                {
                    return new DistributedRateLimitLease(isAcquired: true, retryAfter: null);
                }

                return new DistributedRateLimitLease(
                    isAcquired: false,
                    retryAfter: TimeSpan.FromMilliseconds(remainingTtlMs));
            }

            return new DistributedRateLimitLease(isAcquired: true, retryAfter: null);
        }
        catch (RedisException ex)
        {
            LogRateLimiterUnavailable(_logger, ex, _partitionKey);
            return await _fallbackLimiter.AcquireAsync(permitCount, cancellationToken);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Redis rate limiter unavailable for partition {PartitionKey}; failing open to local fixed-window limiter.")]
    private static partial void LogRateLimiterUnavailable(ILogger logger, RedisException ex, string partitionKey);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fallbackLimiter.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await _fallbackLimiter.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    private sealed class DistributedRateLimitLease(bool isAcquired, TimeSpan? retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => isAcquired;

        public override IEnumerable<string> MetadataNames =>
            retryAfter.HasValue ? [MetadataName.RetryAfter.Name] : [];

        public override bool TryGetMetadata(string metadataName, [NotNullWhen(true)] out object? metadata)
        {
            if (retryAfter.HasValue && string.Equals(metadataName, MetadataName.RetryAfter.Name, StringComparison.Ordinal))
            {
                metadata = retryAfter.Value;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
