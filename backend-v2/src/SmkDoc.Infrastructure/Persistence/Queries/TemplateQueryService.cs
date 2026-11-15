using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class TemplateQueryService(AppDbContext context) : ITemplateQueryService
{
    private readonly AppDbContext _context = context;

    public async Task<(IReadOnlyList<TemplateResultDto> Items, long? TotalCount)> ListPagedAsync(
        string? searchTerm,
        string? category,
        bool? isActive,
        int page,
        int limit,
        bool includeTotal,
        CancellationToken ct)
    {
        IQueryable<Template> query = _context.Templates
            .AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(t => t.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var trimmedCategory = category.Trim();
            query = query.Where(t => t.Category == trimmedCategory);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = $"%{searchTerm.Trim()}%";
            query = query.Where(t =>
                EF.Functions.ILike(EF.Property<string>(t, "Name"), term) ||
                EF.Functions.ILike(EF.Property<string>(t, "Slug"), term));
        }

        long? totalCount = includeTotal ? await query.LongCountAsync(ct) : null;

        var safePage = page < 1 ? 1 : page;
        var safeLimit = limit < 1 ? 20 : Math.Min(limit, 100);

        var rows = await query
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .Select(t => new
            {
                t.Id,
                t.ProjectId,
                t.Name,
                t.Slug,
                t.Category,
                t.IsActive,
                t.CurrentVersionId,
                CurrentFileFormat = t.CurrentVersion != null ? t.CurrentVersion.FileFormat : null,
                t.CreatedAt,
                t.UpdatedAt
            })
            .ToListAsync(ct);

        var items = rows
            .Select(t => new TemplateResultDto(
                t.Id.Value,
                t.ProjectId.Value,
                t.Name.Value,
                t.Slug.Value,
                t.Category,
                t.IsActive,
                t.CurrentVersionId,
                t.CurrentFileFormat?.Name,
                t.CreatedAt,
                t.UpdatedAt))
            .ToList();

        return (items, totalCount);
    }
}
