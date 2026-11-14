namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Port for caching serialized external SQL dataset results when <c>Dataset.CacheSeconds &gt; 0</c>.
/// </summary>
public interface IDatasetResultCache
{
    ValueTask<string?> GetAsync(string cacheKey, CancellationToken ct);
    ValueTask SetAsync(string cacheKey, string jsonResult, TimeSpan ttl, CancellationToken ct);
}
