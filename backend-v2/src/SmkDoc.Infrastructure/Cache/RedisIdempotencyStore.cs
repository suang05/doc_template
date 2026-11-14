using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Cache;

/// <summary>
/// Multi-pod distributed implementation of <see cref="IIdempotencyStore"/> backed by Redis atomic Lua scripts.
/// Enforces fail-closed semantics (<see cref="DistributedStateUnavailableException"/> => HTTP 503) during lock acquisition
/// to guarantee zero duplicate official/financial document generation across pods.
/// </summary>
public sealed partial class RedisIdempotencyStore(
    IConnectionMultiplexer multiplexer,
    TimeProvider timeProvider,
    ILogger<RedisIdempotencyStore> logger,
    string instancePrefix = RedisConfigurationResolver.DefaultInstancePrefix) : IIdempotencyStore
{
    internal const string AcquireLuaScript = """
        local existing = redis.call('GET', KEYS[1])
        if existing then
            return existing
        end
        redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2])
        return false
        """;

    internal const string RemoveInFlightLuaScript = """
        local existing = redis.call('GET', KEYS[1])
        if not existing then
            return 0
        end
        local ok, decoded = pcall(cjson.decode, existing)
        if not ok or decoded['status'] == 'inFlight' or decoded['status'] == 0 then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public async Task<IdempotencyAcquisitionResult> TryAcquireOrGetAsync(
        string cacheKey,
        string requestFingerprint,
        TimeSpan inFlightTtl,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        ct.ThrowIfCancellationRequested();

        var now = timeProvider.GetUtcNow();
        var inFlightRecord = new IdempotencyRecord(
            cacheKey,
            requestFingerprint,
            IdempotencyStatus.InFlight,
            null,
            null,
            null,
            now);

        var serializedInFlight = JsonSerializer.Serialize(inFlightRecord, SerializerOptions);
        var ttlMs = Math.Max(1L, (long)Math.Ceiling(inFlightTtl.TotalMilliseconds));
        var redisKey = PrefixedKey(cacheKey);

        try
        {
            var result = await multiplexer.GetDatabase().ScriptEvaluateAsync(
                AcquireLuaScript,
                [redisKey],
                [serializedInFlight, ttlMs]);

            if (!result.IsNull)
            {
                var existingJson = (string?)result;
                if (!string.IsNullOrWhiteSpace(existingJson))
                {
                    var existingRecord = JsonSerializer.Deserialize<IdempotencyRecord>(existingJson, SerializerOptions);
                    if (existingRecord is not null)
                    {
                        return IdempotencyAcquisitionResult.Conflict(existingRecord);
                    }
                }
            }

            return IdempotencyAcquisitionResult.Acquired();
        }
        catch (RedisException ex)
        {
            LogAcquireLockUnavailable(logger, ex, cacheKey);
            throw new DistributedStateUnavailableException(
                "Distributed idempotency store is temporarily unavailable. Retry shortly.",
                ex);
        }
    }

    public async Task SaveCompletedAsync(
        string cacheKey,
        string requestFingerprint,
        int statusCode,
        string responseJson,
        IReadOnlyDictionary<string, string>? headers,
        TimeSpan completedTtl,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        ct.ThrowIfCancellationRequested();

        var now = timeProvider.GetUtcNow();
        var completedRecord = new IdempotencyRecord(
            cacheKey,
            requestFingerprint,
            IdempotencyStatus.Completed,
            statusCode,
            responseJson,
            headers,
            now);

        var serializedCompleted = JsonSerializer.Serialize(completedRecord, SerializerOptions);
        var ttl = TimeSpan.FromMilliseconds(Math.Max(1L, (long)Math.Ceiling(completedTtl.TotalMilliseconds)));

        try
        {
            await multiplexer.GetDatabase().StringSetAsync(PrefixedKey(cacheKey), serializedCompleted, ttl);
        }
        catch (RedisException ex)
        {
            LogPersistCompletedFailed(logger, ex, cacheKey);
        }
    }

    public async Task RemoveAsync(string cacheKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }

        try
        {
            await multiplexer.GetDatabase().ScriptEvaluateAsync(
                RemoveInFlightLuaScript,
                [PrefixedKey(cacheKey)]);
        }
        catch (RedisException ex)
        {
            LogRollbackInFlightFailed(logger, ex, cacheKey);
        }
    }

    private RedisKey PrefixedKey(string cacheKey) => $"{instancePrefix}{cacheKey}";

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Redis unavailable while acquiring idempotency lock for {CacheKey}")]
    private static partial void LogAcquireLockUnavailable(ILogger logger, RedisException ex, string cacheKey);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to persist completed idempotency response in Redis for {CacheKey}; returning committed response to caller.")]
    private static partial void LogPersistCompletedFailed(ILogger logger, RedisException ex, string cacheKey);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to roll back in-flight idempotency key {CacheKey} in Redis; lock will expire via TTL.")]
    private static partial void LogRollbackInFlightFailed(ILogger logger, RedisException ex, string cacheKey);

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
