using SmkDoc.Application.Modules.Rendering.Logs.DTOs;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Read-optimized query port for paged generation log summaries without hydrating heavy JSONB columns.
/// </summary>
public interface IGenerationLogQueryService
{
    Task<(IReadOnlyList<GenerationLogDto> Items, int TotalCount)> ListPagedAsync(
        string? callerApp,
        int page,
        int limit,
        CancellationToken ct = default);
}
