using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class ProjectQueryService(AppDbContext context) : IProjectQueryService
{
    public async Task<(IReadOnlyList<ProjectResultDto> Items, long? TotalCount)> ListAccessiblePagedAsync(
        UserId userId,
        DateTimeOffset now,
        string? searchTerm,
        int page,
        int limit,
        bool includeTotal,
        CancellationToken ct)
    {
        var memberProjectIds = context.UserProjectRoles
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.ProjectId);

        var grantProjectIds = context.BreakGlassGrants
            .AsNoTracking()
            .Where(g => g.SuperAdminUserId == userId &&
                        g.RevokedAt == null &&
                        g.GrantedAt <= now &&
                        g.ExpiresAt > now)
            .Select(g => g.TargetProjectId);

        var accessibleIds = memberProjectIds.Union(grantProjectIds);

        var query = context.Projects
            .AsNoTracking()
            .Where(p => p.IsActive && accessibleIds.Contains(p.Id));

        return await PaginateProjectsAsync(query, searchTerm, page, limit, includeTotal, ct);
    }

    public async Task<(IReadOnlyList<ProjectResultDto> Items, long? TotalCount)> ListAllControlPlanePagedAsync(
        string? searchTerm,
        int page,
        int limit,
        bool includeTotal,
        CancellationToken ct)
    {
        var query = context.Projects.AsNoTracking();
        return await PaginateProjectsAsync(query, searchTerm, page, limit, includeTotal, ct);
    }

    private static async Task<(IReadOnlyList<ProjectResultDto> Items, long? TotalCount)> PaginateProjectsAsync(
        IQueryable<Project> query,
        string? searchTerm,
        int page,
        int limit,
        bool includeTotal,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = $"%{searchTerm.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike((string)(object)p.Name, term) ||
                EF.Functions.ILike((string)(object)p.Slug, term));
        }

        long? totalCount = includeTotal ? await query.LongCountAsync(ct) : null;

        var safePage = page < 1 ? 1 : page;
        var safeLimit = limit < 1 ? 20 : Math.Min(limit, 100);

        var projects = await query
            .OrderBy(p => p.Name)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToListAsync(ct);

        var items = projects
            .Select(p => p.ToResultDto())
            .ToList();

        return (items, totalCount);
    }
}
