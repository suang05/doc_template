using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class BreakGlassGrantReader(AppDbContext context) : IBreakGlassGrantReader
{
    public Task<bool> HasActiveGrantAsync(
        UserId superAdminUserId,
        ProjectId targetProjectId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        return context.BreakGlassGrants
            .AsNoTracking()
            .AnyAsync(
                g => g.SuperAdminUserId == superAdminUserId &&
                     g.TargetProjectId == targetProjectId &&
                     g.RevokedAt == null &&
                     g.GrantedAt <= now &&
                     g.ExpiresAt > now,
                ct);
    }
}
