using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class TemplateRepository(AppDbContext context) : ITemplateRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Template?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var templateId = new TemplateId(id);
        return await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);
    }

    public async Task<Template?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        var templateId = new TemplateId(id);
        return await _context.Templates
            .AsSplitQuery()
            .Include(t => t.CurrentVersion)
            .Include(t => t.Versions)
            .Include(t => t.FieldMappings)
            .Include(t => t.TemplateDatasets)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);
    }

    public async Task<Template?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }

    public async Task<Template?> GetBySlugWithDetailsAsync(string slug, CancellationToken ct = default)
    {
        return await _context.Templates
            .AsSplitQuery()
            .Include(t => t.CurrentVersion)
            .Include(t => t.Versions)
            .Include(t => t.FieldMappings)
            .Include(t => t.TemplateDatasets)
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
    {
        return await _context.Templates
            .AnyAsync(t => t.Slug == slug, ct);
    }

    public async Task<IReadOnlyList<Template>> ListAsync(CancellationToken ct = default)
    {
        return await _context.Templates
            .AsNoTracking()
            .Include(t => t.CurrentVersion)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<TemplateVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct = default)
    {
        return await _context.Templates
            .SelectMany(t => t.Versions)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
    }

    public async Task<TemplateVersion?> GetCurrentVersionAsync(Guid templateId, CancellationToken ct = default)
    {
        var tId = new TemplateId(templateId);
        return await _context.Templates
            .Where(t => t.Id == tId)
            .Select(t => t.CurrentVersion)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TemplateVersion>> ListVersionsAsync(Guid templateId, CancellationToken ct = default)
    {
        var tId = new TemplateId(templateId);
        return await _context.Templates
            .AsNoTracking()
            .Where(t => t.Id == tId)
            .SelectMany(t => t.Versions)
            .OrderByDescending(v => v.Version)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Template template, CancellationToken ct = default)
    {
        await _context.Templates.AddAsync(template, ct);
    }

    public async Task AddVersionAsync(TemplateVersion version, CancellationToken ct = default)
    {
        await _context.TemplateVersions.AddAsync(version, ct);
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
