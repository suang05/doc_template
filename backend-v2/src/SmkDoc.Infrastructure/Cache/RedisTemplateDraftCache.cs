using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Cache;

public sealed class RedisTemplateDraftCache(
    IConnectionMultiplexer multiplexer,
    IStorageService storageService,
    ILogger<RedisTemplateDraftCache> logger,
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

        var normalizedExt = InMemoryTemplateDraftCache.NormalizeExtension(fileExtension);
        var draftId = Guid.NewGuid().ToString("N");
        var storageKey = StorageBuckets.DraftTemplateKey(projectId, draftId, normalizedExt);
        var uploadedKey = await storageService.UploadAsync(
            StorageBuckets.Drafts,
            storageKey,
            contentStream,
            InMemoryTemplateDraftCache.ContentTypeFromExtension(normalizedExt),
            ct);

        var resolvedStorageKey = string.IsNullOrWhiteSpace(uploadedKey) ? storageKey : uploadedKey;
        var entry = new TemplateDraftEntry(
            draftId,
            projectId.Value,
            resolvedStorageKey,
            fileName,
            normalizedExt,
            placeholders);

        try
        {
            var json = JsonSerializer.Serialize(entry, SerializerOptions);
            await multiplexer.GetDatabase().StringSetAsync(Key(projectId, draftId), json, Ttl);
            return draftId;
        }
        catch (RedisException ex)
        {
            logger.LogError(ex, "Redis unavailable while storing draft metadata for DraftId {DraftId}", draftId);
            await TryCompensatingDeleteAsync(resolvedStorageKey, draftId);
            throw new DistributedStateUnavailableException(
                "Distributed template draft cache is temporarily unavailable. Retry shortly.",
                ex);
        }
        catch
        {
            await TryCompensatingDeleteAsync(resolvedStorageKey, draftId);
            throw;
        }
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

        RedisValue raw;
        try
        {
            raw = await multiplexer.GetDatabase().StringGetSetExpiryAsync(Key(projectId, draftId), Ttl);
        }
        catch (RedisException ex)
        {
            logger.LogError(ex, "Redis unavailable while reading draft metadata for DraftId {DraftId}", draftId);
            throw new DistributedStateUnavailableException(
                "Distributed template draft cache is temporarily unavailable. Retry shortly.",
                ex);
        }

        if (!raw.HasValue)
        {
            return null;
        }

        var entry = JsonSerializer.Deserialize<TemplateDraftEntry>((string)raw!, SerializerOptions);
        if (entry is null || entry.ProjectId != projectId.Value)
        {
            return null;
        }

        return entry;
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

        TemplateDraftEntry? entry = null;
        try
        {
            var raw = await multiplexer.GetDatabase().StringGetDeleteAsync(Key(projectId, draftId));
            if (raw.HasValue)
            {
                entry = JsonSerializer.Deserialize<TemplateDraftEntry>((string)raw!, SerializerOptions);
            }
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Failed to delete draft metadata from Redis for DraftId {DraftId}", draftId);
        }

        if (entry is not null && entry.ProjectId == projectId.Value)
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

    private string Key(ProjectId projectId, string draftId) =>
        $"{instancePrefix}tmpl_draft:{projectId.Value:N}:{draftId.Trim()}";

    private async Task TryCompensatingDeleteAsync(string storageKey, string draftId)
    {
        try
        {
            await storageService.DeleteAsync(StorageBuckets.Drafts, storageKey, CancellationToken.None);
        }
        catch (Exception cleanupEx)
        {
            logger.LogWarning(
                cleanupEx,
                "Compensating delete failed for orphaned draft object {StorageKey} (DraftId {DraftId})",
                storageKey,
                draftId);
        }
    }
}
