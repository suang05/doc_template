using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class FieldMappingRepository(AppDbContext context) : IFieldMappingRepository
{
    private readonly AppDbContext _context = context;

    public async Task<List<FieldMapping>> GetByTemplateIdAsync(Guid templateId, CancellationToken ct = default)
    {
        return await _context.FieldMappings
            .Where(m => m.TemplateId == templateId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);
    }

    public async Task AddAsync(FieldMapping mapping, CancellationToken ct = default)
    {
        await _context.FieldMappings.AddAsync(mapping, ct);
    }

    public void Remove(FieldMapping mapping)
    {
        _context.FieldMappings.Remove(mapping);
    }

    public void RemoveRange(IEnumerable<FieldMapping> mappings)
    {
        _context.FieldMappings.RemoveRange(mappings);
    }
}
