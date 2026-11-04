using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Models;
using SmkDoc.Infrastructure.Cache;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Queue;

/// <summary>
/// Distributed enterprise queue implementation of <see cref="IDocumentRenderQueue"/>
/// backed by Redis List and Hash with 24-hour TTL.
/// </summary>
public sealed class RedisDocumentRenderQueue(
    IConnectionMultiplexer multiplexer,
    ILogger<RedisDocumentRenderQueue> logger,
    string instancePrefix = RedisConfigurationResolver.DefaultInstancePrefix) : IDocumentRenderQueue
{
    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RedisKey _queueKey = $"{instancePrefix}render:queue";

    public async Task<Guid> EnqueueAsync(DocumentJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ct.ThrowIfCancellationRequested();

        var db = multiplexer.GetDatabase();
        var jobKey = PrefixedJobKey(job.JobId);
        var json = JsonSerializer.Serialize(job, SerializerOptions);

        try
        {
            await db.StringSetAsync(jobKey, json, JobTtl);
            await db.ListLeftPushAsync(_queueKey, job.JobId.ToString());
            return job.JobId;
        }
        catch (RedisException ex)
        {
            logger.LogError(ex, "Failed to enqueue document job {JobId} in Redis", job.JobId);
            throw;
        }
    }

    public async Task<DocumentJob?> DequeueAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var db = multiplexer.GetDatabase();

        try
        {
            var rawJobId = await db.ListRightPopAsync(_queueKey);
            if (rawJobId.IsNullOrEmpty || !Guid.TryParse(rawJobId.ToString(), out var jobId))
            {
                return null;
            }

            return await GetJobAsync(jobId, ct);
        }
        catch (RedisException ex)
        {
            logger.LogError(ex, "Failed to dequeue next document job from Redis");
            throw;
        }
    }

    public async Task<DocumentJob?> GetJobAsync(Guid jobId, CancellationToken ct = default)
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
            logger.LogError(ex, "Failed to retrieve document job {JobId} from Redis", jobId);
            throw;
        }
    }

    public async Task UpdateJobAsync(DocumentJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ct.ThrowIfCancellationRequested();

        var db = multiplexer.GetDatabase();
        var jobKey = PrefixedJobKey(job.JobId);
        var json = JsonSerializer.Serialize(job, SerializerOptions);

        try
        {
            await db.StringSetAsync(jobKey, json, JobTtl);
        }
        catch (RedisException ex)
        {
            logger.LogError(ex, "Failed to update document job {JobId} in Redis", job.JobId);
            throw;
        }
    }

    private RedisKey PrefixedJobKey(Guid jobId) => $"{instancePrefix}render:jobs:{jobId}";
}

