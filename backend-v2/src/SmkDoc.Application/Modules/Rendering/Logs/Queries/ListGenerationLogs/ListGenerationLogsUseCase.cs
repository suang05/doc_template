using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

public sealed class ListGenerationLogsUseCase(IGenerationLogRepository logRepo)
    : IUseCase<ListGenerationLogsQuery, GenerationLogPagedResult>
{
    public async Task<GenerationLogPagedResult> ExecuteAsync(ListGenerationLogsQuery query, CancellationToken ct = default)
    {
        int page = Math.Max(1, query.Page);
        int limit = Math.Clamp(query.Limit, 1, 200);

        var (logs, total) = await logRepo.ListPagedAsync(query.CallerApp, page, limit, ct);

        var dtos = logs.Select(l => new GenerationLogDto(
            l.Id,
            l.TemplateId?.Value,
            l.TemplateVersionId,
            l.ApiKeyId?.Value,
            l.CallerApp,
            l.TriggerSource,
            l.OutputFormat?.Extension,
            l.FileSizeBytes,
            l.PageCount,
            l.DurationMs,
            l.Status,
            l.ErrorMsg,
            l.CreatedAt)).ToList();

        return new GenerationLogPagedResult(dtos, total, page, limit);
    }
}
