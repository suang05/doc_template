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

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        await _context.Projects.AddAsync(project, ct);
    }

    public void Update(Project project)
    {
        _context.Projects.Update(project);
    }
}
