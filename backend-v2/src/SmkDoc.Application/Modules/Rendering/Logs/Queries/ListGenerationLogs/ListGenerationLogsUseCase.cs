using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

public sealed class ListGenerationLogsUseCase(IGenerationLogQueryService logQueryService)
    : IUseCase<ListGenerationLogsQuery, GenerationLogPagedResult>
{
    public async Task<GenerationLogPagedResult> ExecuteAsync(
        ListGenerationLogsQuery query,
        CancellationToken ct = default)
    {
        int page = Math.Max(1, query.Page);
        int limit = Math.Clamp(query.Limit, 1, 200);

        var (items, total) = await logQueryService.ListPagedAsync(query.CallerApp, page, limit, ct);

        return new GenerationLogPagedResult(items, total, page, limit);
    }
}
