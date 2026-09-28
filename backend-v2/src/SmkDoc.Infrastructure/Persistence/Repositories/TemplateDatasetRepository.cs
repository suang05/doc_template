using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class TemplateDatasetRepository(AppDbContext context) : ITemplateDatasetRepository
{
    private readonly AppDbContext _context = context;

    public async Task<List<TemplateDataset>> GetByTemplateIdAsync(Guid templateId, CancellationToken ct = default)
    {
        return await _context.TemplateDatasets
            .Where(td => td.TemplateId == templateId)
            .OrderBy(td => td.SortOrder)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TemplateDataset templateDataset, CancellationToken ct = default)
    {
        await _context.TemplateDatasets.AddAsync(templateDataset, ct);
    }

    public void Remove(TemplateDataset templateDataset)
    {
        _context.TemplateDatasets.Remove(templateDataset);
    }

    public void RemoveRange(IEnumerable<TemplateDataset> templateDatasets)
    {
        _context.TemplateDatasets.RemoveRange(templateDatasets);
    }
}
