using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class GenerationLogQueryService(AppDbContext context) : IGenerationLogQueryService
{
    private readonly AppDbContext _context = context;

    public async Task<(IReadOnlyList<GenerationLogDto> Items, int TotalCount)> ListPagedAsync(
        string? callerApp,
        int page,
        int limit,
        CancellationToken ct = default)
    {
        IQueryable<GenerationLog> query = _context.GenerationLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(callerApp))
        {
            var trimmedCallerApp = callerApp.Trim();
            query = query.Where(l => l.CallerApp == trimmedCallerApp);
        }

        var totalCount = await query.CountAsync(ct);

        var safePage = page < 1 ? 1 : page;
        var safeLimit = limit < 1 ? 20 : (limit > 200 ? 200 : limit);

        var rows = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .Select(l => new
            {
                l.Id,
                l.TemplateId,
                l.TemplateVersionId,
                l.ApiKeyId,
                l.CallerApp,
                l.TriggerSource,
                l.OutputFormat,
                l.FileSizeBytes,
                l.PageCount,
                l.DurationMs,
                l.Status,
                l.ErrorMsg,
                l.CreatedAt
            })
            .ToListAsync(ct);

        var items = rows
            .Select(l => new GenerationLogDto(
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
                l.CreatedAt))
            .ToList();

        return (items, totalCount);
    }
}
