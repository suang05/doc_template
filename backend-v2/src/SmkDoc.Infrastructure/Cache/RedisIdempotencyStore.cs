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
public sealed class RedisIdempotencyStore(
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
        CancellationToken ct = default)
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
            logger.LogError(ex, "Redis unavailable while acquiring idempotency lock for {CacheKey}", cacheKey);
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
        CancellationToken ct = default)
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
            logger.LogError(
                ex,
                "Failed to persist completed idempotency response in Redis for {CacheKey}; returning committed response to caller.",
                cacheKey);
        }
    }

    public async Task RemoveAsync(string cacheKey, CancellationToken ct = default)
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
            logger.LogWarning(
                ex,
                "Failed to roll back in-flight idempotency key {CacheKey} in Redis; lock will expire via TTL.",
                cacheKey);
        }
    }

    private RedisKey PrefixedKey(string cacheKey) => $"{instancePrefix}{cacheKey}";

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
