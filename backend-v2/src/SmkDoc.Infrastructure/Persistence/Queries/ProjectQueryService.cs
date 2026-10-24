using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class ProjectQueryService(AppDbContext context) : IProjectQueryService
{
    private readonly AppDbContext _context = context;

    public async Task<(IReadOnlyList<ProjectResultDto> Items, int TotalCount)> ListPagedByUserAsync(
        UserId userId,
        bool isSuperAdmin,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        IQueryable<Project> query = _context.Projects.AsNoTracking().Where(p => p.IsActive);

        if (!isSuperAdmin)
        {
            var userProjectIds = _context.UserProjectRoles
                .Where(r => r.UserId == userId)
                .Select(r => r.ProjectId);

            query = query.Where(p => userProjectIds.Contains(p.Id));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = $"%{searchTerm.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(EF.Property<string>(p, "Name"), term) ||
                EF.Functions.ILike(EF.Property<string>(p, "Slug"), term));
        }

        var totalCount = await query.CountAsync(ct);

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : (pageSize > 100 ? 100 : pageSize);

        var projects = await query
            .OrderBy(p => p.Name)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        var items = projects
            .Select(p => new ProjectResultDto(p.Id.Value, p.Name.Value, p.Slug.Value, p.IsActive, p.CreatedAt))
            .ToList();

        return (items, totalCount);
    }
}
