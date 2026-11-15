using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface IFontRepository
{
    Task<Font?> GetByIdAsync(FontId id, CancellationToken ct);
    Task<IReadOnlyList<Font>> ListAsync(ProjectId? projectId, CancellationToken ct);
    Task AddAsync(Font font, CancellationToken ct);
    void Remove(Font font);
}
