using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class FontRepository(AppDbContext context) : IFontRepository
{
    public async Task<Font?> GetByIdAsync(FontId id, CancellationToken ct)
    {
        return await context.Fonts.FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<IReadOnlyList<Font>> ListAsync(ProjectId? projectId, CancellationToken ct)
    {
        var query = context.Fonts.AsNoTracking();

        query = projectId.HasValue
            ? query.Where(f => f.ProjectId == null || f.ProjectId == projectId.Value)
            : query.Where(f => f.ProjectId == null);

        return await query
            .OrderBy(f => f.FamilyName)
            .ThenBy(f => f.Weight)
            .ThenBy(f => f.Style)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Font font, CancellationToken ct)
    {
        await context.Fonts.AddAsync(font, ct);
    }

    public void Remove(Font font)
    {
        context.Fonts.Remove(font);
    }
}
