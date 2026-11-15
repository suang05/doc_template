using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface IBreakGlassGrantRepository
{
    Task<BreakGlassGrant?> GetByIdAsync(BreakGlassGrantId id, CancellationToken ct);
    Task<bool> HasActiveGrantAsync(UserId superAdminUserId, ProjectId targetProjectId, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<BreakGlassGrant>> ListActiveGrantsAsync(UserId? superAdminUserId, ProjectId? targetProjectId, DateTimeOffset now, CancellationToken ct);
    Task AddAsync(BreakGlassGrant grant, CancellationToken ct);
    void Update(BreakGlassGrant grant);
}
