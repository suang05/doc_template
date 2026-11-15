using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class BreakGlassGrantRepository(AppDbContext context) : IBreakGlassGrantRepository
{
    public async Task<BreakGlassGrant?> GetByIdAsync(BreakGlassGrantId id, CancellationToken ct)
    {
        return await context.BreakGlassGrants
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<bool> HasActiveGrantAsync(
        UserId superAdminUserId,
        ProjectId targetProjectId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        return await context.BreakGlassGrants
            .AsNoTracking()
            .AnyAsync(
                g => g.SuperAdminUserId == superAdminUserId &&
                     g.TargetProjectId == targetProjectId &&
                     g.RevokedAt == null &&
                     g.GrantedAt <= now &&
                     g.ExpiresAt > now,
                ct);
    }

    public async Task<IReadOnlyList<BreakGlassGrant>> ListActiveGrantsAsync(
        UserId? superAdminUserId,
        ProjectId? targetProjectId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var query = context.BreakGlassGrants
            .AsNoTracking()
            .Where(g => g.RevokedAt == null && g.GrantedAt <= now && g.ExpiresAt > now);

        if (superAdminUserId.HasValue)
        {
            var adminId = superAdminUserId.Value;
            query = query.Where(g => g.SuperAdminUserId == adminId);
        }

        if (targetProjectId.HasValue)
        {
            var projectId = targetProjectId.Value;
            query = query.Where(g => g.TargetProjectId == projectId);
        }

        return await query
            .OrderByDescending(g => g.GrantedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(BreakGlassGrant grant, CancellationToken ct)
    {
        await context.BreakGlassGrants.AddAsync(grant, ct);
    }

    public void Update(BreakGlassGrant grant)
    {
        context.BreakGlassGrants.Update(grant);
    }
}
