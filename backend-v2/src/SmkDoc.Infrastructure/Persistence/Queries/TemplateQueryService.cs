using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class TemplateQueryService(AppDbContext context) : ITemplateQueryService
{
    private readonly AppDbContext _context = context;

    public async Task<(IReadOnlyList<TemplateResultDto> Items, int TotalCount)> ListPagedAsync(
        ProjectId projectId,
        string? searchTerm,
        string? category,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        IQueryable<Template> query = _context.Templates
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId);

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

        var totalCount = await query.CountAsync(ct);

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : (pageSize > 100 ? 100 : pageSize);

        var templates = await query
            .Include(t => t.CurrentVersion)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        var items = templates
            .Select(t => new TemplateResultDto(
                t.Id.Value,
                t.ProjectId.Value,
                t.Name.Value,
                t.Slug.Value,
                t.Category,
                t.IsActive,
                t.CurrentVersionId,
                t.CurrentVersion?.FileFormat?.Name,
                t.CreatedAt,
                t.UpdatedAt
            ))
            .ToList();

        return (items, totalCount);
    }
}
