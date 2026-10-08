using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class UserWorkspaceQueryService(AppDbContext context) : IUserWorkspaceQueryService
{
    public async Task<IReadOnlyList<AccessibleProjectDto>> GetAccessibleProjectsAsync(
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct = default)
    {
        if (isSuperAdmin)
        {
            return await context.Projects
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new AccessibleProjectDto(p.Id, p.Name, p.Slug, "Admin"))
                .ToListAsync(ct);
        }

        var results = await (
            from r in context.UserProjectRoles.AsNoTracking()
            where r.UserId == userId
            join p in context.Projects.AsNoTracking() on r.ProjectId equals p.Id
            where p.IsActive
            orderby p.Name
            select new
            {
                p.Id,
                p.Name,
                p.Slug,
                Role = r.Role.Name
            }
        ).ToListAsync(ct);

        return results
            .Select(x => new AccessibleProjectDto(x.Id, x.Name, x.Slug, x.Role))
            .ToList();
    }

    public async Task<IReadOnlyList<ApiKeyDto>> GetActiveApiKeysAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var keys = await context.ApiKeys
            .AsNoTracking()
            .Where(k => k.ProjectId == projectId && k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        return keys
            .Select(k => new ApiKeyDto(
                k.Id,
                k.Name.Value,
                k.CallerApp,
                k.IsActive,
                k.LastUsedAt,
                k.CreatedAt,
                k.Scope.Name))
            .ToList();
    }
}
