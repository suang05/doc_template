using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

public sealed class ListGenerationLogsUseCase(IRepository<GenerationLog> repo)
    : IUseCase<ListGenerationLogsQuery, GenerationLogPagedResult>
{
    private readonly IRepository<GenerationLog> _repo = repo;

    public async Task<GenerationLogPagedResult> ExecuteAsync(ListGenerationLogsQuery query, CancellationToken ct = default)
    {
        int page = Math.Max(1, query.Page);
        int limit = Math.Clamp(query.Limit, 1, 200);

        var (logs, total) = await _repo.PagedListAsync(
            predicate: string.IsNullOrWhiteSpace(query.CallerApp)
                ? null
                : l => l.CallerApp == query.CallerApp,
            orderBy: l => l.CreatedAt,
            descending: true,
            page: page,
            limit: limit,
            ct: ct);

        var dtos = logs.Select(l => new GenerationLogDto(
            l.Id,
            l.TemplateId,
            l.TemplateVersionId,
            l.ApiKeyId,
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
