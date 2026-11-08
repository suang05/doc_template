using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Cache;

/// <summary>
/// Unified hybrid L1/L2 template draft cache implementation of <see cref="ITemplateDraftCache"/>.
/// Combines in-process L1 memory caching (sub-millisecond instant preview) with L2 Redis synchronization
/// when Redis is configured. Gracefully falls back to pure L1 memory caching in standalone local environments.
/// </summary>
public sealed class HybridTemplateDraftCache(
    IConnectionMultiplexer? multiplexer,
    IStorageService storageService,
    ILogger<HybridTemplateDraftCache> logger,
    IMemoryCache memoryCache,
    string instancePrefix = RedisConfigurationResolver.DefaultInstancePrefix) : ITemplateDraftCache
{
    public const long MaxFileSizeBytes = 20L * 1024 * 1024;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> StoreAsync(
        ProjectId projectId,
        Stream contentStream,
        string fileName,
        string fileExtension,
        IReadOnlyList<string> placeholders,
        CancellationToken ct = default)
    {
        if (projectId == ProjectId.Empty)
        {
            throw new ForbiddenException("Active project context is required to store a template draft.");
        }

        ArgumentNullException.ThrowIfNull(contentStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ct.ThrowIfCancellationRequested();

        if (!contentStream.CanSeek)
        {
            throw new InvalidOperationException("Draft contentStream must be seekable with known Length.");
        }

        long payloadBytes = contentStream.Length - contentStream.Position;
        if (payloadBytes > MaxFileSizeBytes)
        {
            throw new DomainValidationException(
                $"Draft file size ({payloadBytes / (1024 * 1024)} MB) exceeds the 20 MB limit.");
        }

        var normalizedExt = NormalizeExtension(fileExtension);
        var draftId = Guid.NewGuid().ToString("N");
        var storageKey = StorageBuckets.DraftTemplateKey(projectId, draftId, normalizedExt);
        var uploadedKey = await storageService.UploadAsync(
            StorageBuckets.Drafts,
            storageKey,
            contentStream,
            ContentTypeFromExtension(normalizedExt),
            ct);

        var resolvedStorageKey = string.IsNullOrWhiteSpace(uploadedKey) ? storageKey : uploadedKey;
        var entry = new TemplateDraftEntry(
            draftId,
            projectId.Value,
            resolvedStorageKey,
            fileName,
            normalizedExt,
            placeholders);

        // 1. Store in L1 In-Memory Cache for sub-millisecond local retrieval
        memoryCache.Set(L1Key(projectId, draftId), entry, new MemoryCacheEntryOptions
        {
            SlidingExpiration = Ttl
        });

        // 2. Synchronize to L2 Distributed Redis store if multiplexer is available
        if (multiplexer is not null)
        {
            var db = multiplexer.GetDatabase();
            var redisKey = PrefixedRedisKey(projectId, draftId);
            var json = JsonSerializer.Serialize(entry, SerializerOptions);

            try
            {
                await db.StringSetAsync(redisKey, json, Ttl);
            }
            catch (RedisException ex)
            {
                logger.LogWarning(ex, "Failed to synchronize draft {DraftId} to L2 Redis for project {ProjectId}", draftId, projectId.Value);
            }
        }

        return draftId;
    }

    public async Task<TemplateDraftEntry?> GetAsync(
        ProjectId projectId,
        string draftId,
        CancellationToken ct = default)
    {
        if (projectId == ProjectId.Empty || string.IsNullOrWhiteSpace(draftId))
        {
            return null;
        }

        ct.ThrowIfCancellationRequested();

        // 1. Fast Path: Check L1 memory cache first (< 0.05 ms latency)
        string l1CacheKey = L1Key(projectId, draftId);
        if (memoryCache.TryGetValue(l1CacheKey, out TemplateDraftEntry? localEntry) &&
            localEntry is not null &&
            localEntry.ProjectId == projectId.Value)
        {
            return localEntry;
        }

        if (multiplexer is null)
        {
            return null;
        }

        // 2. Fallback Path: Retrieve from L2 Redis distributed store (handles cross-pod failover)
        var db = multiplexer.GetDatabase();
        var redisKey = PrefixedRedisKey(projectId, draftId);

        try
        {
            var rawJson = await db.StringGetAsync(redisKey);
            if (rawJson.IsNullOrEmpty)
            {
                return null;
            }

            var entry = JsonSerializer.Deserialize<TemplateDraftEntry>(rawJson.ToString(), SerializerOptions);
            if (entry is null || entry.ProjectId != projectId.Value)
            {
                return null;
            }

            // Backfill L1 memory cache on L2 hit
            memoryCache.Set(l1CacheKey, entry, new MemoryCacheEntryOptions
            {
                SlidingExpiration = Ttl
            });

            return entry;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Failed to retrieve draft {DraftId} from L2 Redis for project {ProjectId}", draftId, projectId.Value);
            return null;
        }
    }

    public Task<Stream> OpenStreamAsync(
        TemplateDraftEntry draft,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return storageService.DownloadAsync(StorageBuckets.Drafts, draft.StorageKey, ct);
    }

    public async Task RemoveAsync(
        ProjectId projectId,
        string draftId,
        CancellationToken ct = default)
    {
        if (projectId == ProjectId.Empty || string.IsNullOrWhiteSpace(draftId))
        {
            return;
        }

        // 1. Evict from L1 memory cache
        memoryCache.Remove(L1Key(projectId, draftId));

        TemplateDraftEntry? entry = null;

        // 2. Evict from L2 Redis if available
        if (multiplexer is not null)
        {
            var db = multiplexer.GetDatabase();
            var redisKey = PrefixedRedisKey(projectId, draftId);

            try
            {
                var rawJson = await db.StringGetDeleteAsync(redisKey);
                if (!rawJson.IsNullOrEmpty)
                {
                    entry = JsonSerializer.Deserialize<TemplateDraftEntry>(rawJson.ToString(), SerializerOptions);
                }
            }
            catch (RedisException ex)
            {
                logger.LogWarning(ex, "Failed to delete draft {DraftId} from L2 Redis for project {ProjectId}", draftId, projectId.Value);
            }
        }

        // 3. Delete binary object from MinIO drafts bucket
        if (entry is not null)
        {
            try
            {
                await storageService.DeleteAsync(StorageBuckets.Drafts, entry.StorageKey, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Failed to delete temporary draft object {StorageKey} for DraftId {DraftId}",
                    entry.StorageKey,
                    draftId);
            }
        }
    }

    private static string L1Key(ProjectId projectId, string draftId) =>
        $"tmpl_draft:l1:{projectId.Value:N}:{draftId.Trim()}";

    private RedisKey PrefixedRedisKey(ProjectId projectId, string draftId) =>
        $"{instancePrefix}drafts:{projectId.Value:N}:{draftId.Trim()}";

    public static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".html";
        }

        return extension.StartsWith('.')
            ? extension.Trim().ToLowerInvariant()
            : $".{extension.Trim().ToLowerInvariant()}";
    }

    public static string ContentTypeFromExtension(string normalizedExtension) => normalizedExtension switch
    {
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "text/html; charset=utf-8"
    };
}

