using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Cache;

public sealed class InMemoryTemplateDraftCache(
    IMemoryCache cache,
    IStorageService storageService,
    ILogger<InMemoryTemplateDraftCache> logger) : ITemplateDraftCache
{
    public const long MaxFileSizeBytes = 20L * 1024 * 1024;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

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

        cache.Set(Key(projectId, draftId), entry, new MemoryCacheEntryOptions
        {
            SlidingExpiration = Ttl
        });

        return draftId;
    }

    public Task<TemplateDraftEntry?> GetAsync(
        ProjectId projectId,
        string draftId,
        CancellationToken ct = default)
    {
        if (projectId == ProjectId.Empty || string.IsNullOrWhiteSpace(draftId))
        {
            return Task.FromResult<TemplateDraftEntry?>(null);
        }

        if (!cache.TryGetValue(Key(projectId, draftId), out TemplateDraftEntry? entry) ||
            entry is null ||
            entry.ProjectId != projectId.Value)
        {
            return Task.FromResult<TemplateDraftEntry?>(null);
        }

        return Task.FromResult<TemplateDraftEntry?>(entry);
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

        var cacheKey = Key(projectId, draftId);
        cache.TryGetValue(cacheKey, out TemplateDraftEntry? entry);
        cache.Remove(cacheKey);

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

    private static string Key(ProjectId projectId, string draftId) =>
        $"tmpl_draft:{projectId.Value:N}:{draftId.Trim()}";

    internal static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".html";
        }

        return extension.StartsWith('.')
            ? extension.Trim().ToLowerInvariant()
            : $".{extension.Trim().ToLowerInvariant()}";
    }

    internal static string ContentTypeFromExtension(string normalizedExtension) => normalizedExtension switch
    {
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "text/html; charset=utf-8"
    };
}
