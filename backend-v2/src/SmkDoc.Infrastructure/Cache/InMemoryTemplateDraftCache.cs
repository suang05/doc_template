using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Infrastructure.Cache;

public class InMemoryTemplateDraftCache : ITemplateDraftCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    private readonly IMemoryCache _cache;

    public InMemoryTemplateDraftCache(IMemoryCache cache) => _cache = cache;

    public Task<string> StoreAsync(TemplateDraftEntry draft, CancellationToken ct = default)
    {
        if (draft.FileBytes.Length > MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"Draft file size ({draft.FileBytes.Length / 1024 / 1024} MB) exceeds the 20 MB limit.");

        var draftId = Guid.NewGuid().ToString("N");
        _cache.Set(Key(draftId), draft, new MemoryCacheEntryOptions
        {
            SlidingExpiration = Ttl,
        });
        return Task.FromResult(draftId);
    }

    public Task<TemplateDraftEntry?> GetAsync(string draftId, CancellationToken ct = default)
    {
        _cache.TryGetValue(Key(draftId), out TemplateDraftEntry? draft);
        return Task.FromResult(draft);
    }

    public Task RemoveAsync(string draftId, CancellationToken ct = default)
    {
        _cache.Remove(Key(draftId));
        return Task.CompletedTask;
    }

    private static string Key(string draftId) => $"tmpl_draft:{draftId}";
}
