using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class TemplateRepository(AppDbContext context) : ITemplateRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Template?> GetByIdAsync(TemplateId id, CancellationToken ct)
    {
        return await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Template?> GetByIdWithDetailsAsync(TemplateId id, CancellationToken ct)
    {
        return await _context.Templates
            .AsSplitQuery()
            .Include(t => t.CurrentVersion)
            .Include(t => t.Versions)
            .Include(t => t.FieldMappings)
            .Include(t => t.TemplateDatasets)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Template?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        return await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }

    public async Task<Template?> GetBySlugWithDetailsAsync(string slug, CancellationToken ct)
    {
        return await _context.Templates
            .AsSplitQuery()
            .Include(t => t.CurrentVersion)
            .Include(t => t.FieldMappings)
            .Include(t => t.TemplateDatasets)
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        return await _context.Templates
            .AnyAsync(t => t.Slug == slug, ct);
    }

    public async Task<IReadOnlyList<Template>> ListAsync(CancellationToken ct)
    {
        return await _context.Templates
            .AsNoTracking()
            .Include(t => t.CurrentVersion)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<TemplateVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct)
    {
        return await _context.Templates
            .SelectMany(t => t.Versions)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
    }

    public async Task<IReadOnlyList<TemplateVersion>> ListVersionsAsync(TemplateId templateId, CancellationToken ct)
    {
        return await _context.Templates
            .AsNoTracking()
            .Where(t => t.Id == templateId)
            .SelectMany(t => t.Versions)
            .OrderByDescending(v => v.Version)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Template entity, CancellationToken ct)
    {
        await _context.Templates.AddAsync(entity, ct);
    }

    public void Update(Template entity)
    {
        if (_context.Entry(entity).State == EntityState.Detached)
        {
            _context.Templates.Update(entity);
        }
    }

    public void Remove(Template entity)
    {
        _context.Templates.Remove(entity);
    }
}
