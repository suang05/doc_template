using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository(AppDbContext context) : IProjectRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Project?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return await _context.Projects.FirstOrDefaultAsync(p => p.Slug == slug, ct);
    }

    public async Task<Project?> GetBySlugAsync(SmkDoc.Domain.ValueObjects.TemplateSlug slug, CancellationToken ct = default)
    {
        return await _context.Projects.FirstOrDefaultAsync(p => p.Slug == slug, ct);
    }

    public async Task<bool> ExistsBySlugAsync(SmkDoc.Domain.ValueObjects.TemplateSlug slug, CancellationToken ct = default)
    {
        return await _context.Projects.AnyAsync(p => p.Slug == slug, ct);
    }

    public async Task<IReadOnlyList<Project>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idSet = ids.ToHashSet();
        return await _context.Projects
            .Where(p => idSet.Contains(p.Id) && p.IsActive)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Project>> ListActiveAsync(CancellationToken ct = default)
    {
        return await _context.Projects
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Project> Items, int TotalCount)> ListPagedByUserAsync(
        Guid userId,
        bool isSuperAdmin,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        IQueryable<Project> query = _context.Projects.Where(p => p.IsActive);

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

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        await _context.Projects.AddAsync(project, ct);
    }

    public void Update(Project project)
    {
        _context.Projects.Update(project);
    }
}
