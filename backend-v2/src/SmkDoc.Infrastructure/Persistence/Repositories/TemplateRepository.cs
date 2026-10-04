using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class TemplateRepository(AppDbContext context) : ITemplateRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Template?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Template?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Templates
            .Include(t => t.CurrentVersion)
            .Include(t => t.Versions)
            .Include(t => t.FieldMappings)
            .Include(t => t.TemplateDatasets)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Template?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }

    public async Task<Template?> GetBySlugAsync(string slug, Guid? projectId, CancellationToken ct = default)
    {
        return await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Slug == slug && (!projectId.HasValue || t.ProjectId == projectId.Value), ct);
    }

    public async Task<Template?> GetBySlugWithDetailsAsync(string slug, CancellationToken ct = default)
    {
        return await _context.Templates
            .Include(t => t.CurrentVersion)
            .Include(t => t.Versions)
            .Include(t => t.FieldMappings)
            .Include(t => t.TemplateDatasets)
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid projectId, CancellationToken ct = default)
    {
        return await _context.Templates
            .AnyAsync(t => t.Slug == slug && (projectId == Guid.Empty || t.ProjectId == projectId), ct);
    }

    public async Task<IReadOnlyList<Template>> ListAsync(CancellationToken ct = default)
    {
        return await _context.Templates
            .Include(t => t.CurrentVersion)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Template template, CancellationToken ct = default)
    {
        await _context.Templates.AddAsync(template, ct);
    }

    public void Update(Template template)
    {
        _context.Templates.Update(template);
    }

    public void Remove(Template template)
    {
        _context.Templates.Remove(template);
    }
}
