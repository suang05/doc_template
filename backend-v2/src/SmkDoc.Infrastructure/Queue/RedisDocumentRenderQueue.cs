using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Models;
using SmkDoc.Infrastructure.Cache;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Queue;

/// <summary>
/// Distributed enterprise queue implementation of <see cref="IDocumentRenderQueue"/>
/// backed by Redis Streams with Consumer Groups (XADD, XREADGROUP, XACK)
/// providing guaranteed at-least-once document delivery.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Distributed message queue implementation, not System.Collections.Queue.")]
public sealed partial class RedisDocumentRenderQueue(
    IConnectionMultiplexer multiplexer,
    ILogger<RedisDocumentRenderQueue> logger,
    string instancePrefix = RedisConfigurationResolver.DefaultInstancePrefix) : IDocumentRenderQueue
{
    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RedisKey _streamKey = $"{instancePrefix}render:stream";
    private readonly RedisValue _groupName = $"{instancePrefix}render:group";
    private readonly RedisValue _consumerName = $"{Environment.MachineName}_{Environment.ProcessId}";

    private readonly ConcurrentDictionary<Guid, string> _activeStreamIds = new();
    private volatile bool _groupInitialized;

    public async Task<Guid> EnqueueAsync(DocumentJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);
        ct.ThrowIfCancellationRequested();

        var db = multiplexer.GetDatabase();
        var jobKey = PrefixedJobKey(job.JobId);
        var json = JsonSerializer.Serialize(job, SerializerOptions);

        try
        {
            await db.StringSetAsync(jobKey, json, JobTtl);
            await db.StreamAddAsync(_streamKey, [new NameValueEntry("jobId", job.JobId.ToString())]);
            return job.JobId;
        }
        catch (RedisException ex)
        {
            throw new Application.Common.Exceptions.ExternalServiceException(
                "Redis",
                $"Failed to enqueue document job {job.JobId} in Redis Streams.",
                ex);
        }
    }

    public async Task<DocumentJob?> DequeueAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var db = multiplexer.GetDatabase();
        await EnsureConsumerGroupAsync(db);

        try
        {
            // 1. Read new pending messages for this consumer group
            var entries = await db.StreamReadGroupAsync(
                _streamKey,
                _groupName,
                _consumerName,
                ">",
                count: 1);

            // 2. If no new messages, check for pending messages abandoned by crashed workers
            if (entries.Length == 0)
            {
                var pendingInfo = await db.StreamPendingAsync(_streamKey, _groupName);
                if (pendingInfo.PendingMessageCount > 0)
                {
                    entries = await db.StreamReadGroupAsync(
                        _streamKey,
                        _groupName,
                        _consumerName,
                        "0-0",
                        count: 1);
                }
            }

            if (entries.Length == 0)
            {
                return null;
            }

            var entry = entries[0];
            string? rawJobId = null;
            foreach (var nv in entry.Values)
            {
                if (nv.Name == "jobId")
                {
                    rawJobId = nv.Value;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(rawJobId) || !Guid.TryParse(rawJobId, out var jobId))
            {
                await db.StreamAcknowledgeAsync(_streamKey, _groupName, entry.Id);
                await db.StreamDeleteAsync(_streamKey, [entry.Id]);
                return null;
            }

            string streamId = entry.Id.ToString();
            _activeStreamIds[jobId] = streamId;
            await db.StringSetAsync(PrefixedStreamIdKey(jobId), streamId, JobTtl);

            return await GetJobAsync(jobId, ct);
        }
        catch (RedisException ex)
        {
            throw new Application.Common.Exceptions.ExternalServiceException(
                "Redis",
                "Failed to dequeue next document job from Redis Streams.",
                ex);
        }
    }

    public async Task<DocumentJob?> GetJobAsync(Guid jobId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var db = multiplexer.GetDatabase();
        var jobKey = PrefixedJobKey(jobId);

        try
        {
            var rawJson = await db.StringGetAsync(jobKey);
            if (rawJson.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<DocumentJob>(rawJson.ToString(), SerializerOptions);
        }
        catch (RedisException ex)
        {
            throw new Application.Common.Exceptions.ExternalServiceException(
                "Redis",
                $"Failed to retrieve document job {jobId} from Redis.",
                ex);
        }
    }

    public async Task UpdateJobAsync(DocumentJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);
        ct.ThrowIfCancellationRequested();

        var db = multiplexer.GetDatabase();
        var jobKey = PrefixedJobKey(job.JobId);
        var json = JsonSerializer.Serialize(job, SerializerOptions);

        try
        {
            await db.StringSetAsync(jobKey, json, JobTtl);

            if (job.Status is DocumentJobStatus.Completed or DocumentJobStatus.Failed)
            {
                if (!_activeStreamIds.TryRemove(job.JobId, out var streamId))
                {
                    var streamIdKey = PrefixedStreamIdKey(job.JobId);
                    var redisStreamId = await db.StringGetAsync(streamIdKey);
                    if (!redisStreamId.IsNullOrEmpty)
                    {
                        streamId = redisStreamId.ToString();
                        await db.KeyDeleteAsync(streamIdKey);
                    }
                }

                if (!string.IsNullOrEmpty(streamId))
                {
                    await db.StreamAcknowledgeAsync(_streamKey, _groupName, streamId);
                    await db.StreamDeleteAsync(_streamKey, [streamId]);
                }
            }
        }
        catch (RedisException ex)
        {
            throw new Application.Common.Exceptions.ExternalServiceException(
                "Redis",
                $"Failed to update document job {job.JobId} in Redis.",
                ex);
        }
    }

    private async Task EnsureConsumerGroupAsync(IDatabase db)
    {
        if (_groupInitialized) return;

        try
        {
            await db.StreamCreateConsumerGroupAsync(_streamKey, _groupName, "0-0", createStream: true);
            _groupInitialized = true;
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
        {
            _groupInitialized = true;
        }
        catch (RedisException ex)
        {
            LogCreateConsumerGroupFailed(logger, ex, _groupName, _streamKey);
        }
        catch (TimeoutException ex)
        {
            LogCreateConsumerGroupTimedOut(logger, ex, _groupName, _streamKey);
        }
    }

    private RedisKey PrefixedJobKey(Guid jobId) => $"{instancePrefix}render:jobs:{jobId}";
    private RedisKey PrefixedStreamIdKey(Guid jobId) => $"{instancePrefix}render:jobs:{jobId}:stream_id";

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Could not create consumer group {GroupName} on stream {StreamKey}")]
    private static partial void LogCreateConsumerGroupFailed(ILogger logger, RedisException ex, string? groupName, string? streamKey);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Timed out creating consumer group {GroupName} on stream {StreamKey}")]
    private static partial void LogCreateConsumerGroupTimedOut(ILogger logger, TimeoutException ex, string? groupName, string? streamKey);
}
