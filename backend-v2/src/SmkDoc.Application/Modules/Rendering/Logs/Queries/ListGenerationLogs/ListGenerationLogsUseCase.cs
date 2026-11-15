using SmkDoc.Application.Modules.Rendering.Logs.DTOs;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

public sealed class ListGenerationLogsUseCase(IGenerationLogQueryService logQueryService)
    : IUseCase<ListGenerationLogsQuery, GenerationLogPagedResult>
{
    public async Task<GenerationLogPagedResult> ExecuteAsync(
        ListGenerationLogsQuery query,
        CancellationToken ct)
    {
        var (items, total) = await logQueryService.ListPagedAsync(
            query.CallerApp,
            query.Page,
            query.Limit,
            query.IncludeTotal,
            ct);

        return new GenerationLogPagedResult(items, total, query.Page, query.Limit);
    }
}
