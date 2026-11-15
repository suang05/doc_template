using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class UserWorkspaceQueryService(
    AppDbContext context,
    TimeProvider timeProvider) : IUserWorkspaceQueryService
{
    public async Task<IReadOnlyList<AccessibleProjectResultDto>> GetAccessibleProjectsAsync(
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct)
    {
        // Zero Standing Access (ZSA): SuperAdmin status never grants implicit project membership.
        _ = isSuperAdmin;
        var typedUserId = new UserId(userId);
        var now = timeProvider.GetUtcNow();

        var memberProjects = await (
            from r in context.UserProjectRoles.AsNoTracking()
            where r.UserId == typedUserId
            join p in context.Projects.AsNoTracking() on r.ProjectId equals p.Id
            where p.IsActive
            select new
            {
                Id = p.Id.Value,
                Name = p.Name.Value,
                Slug = p.Slug.Value,
                Role = r.Role.Name
            }
        ).ToListAsync(ct);

        var breakGlassProjects = await (
            from g in context.BreakGlassGrants.AsNoTracking()
            where g.SuperAdminUserId == typedUserId &&
                  g.RevokedAt == null &&
                  g.GrantedAt <= now &&
                  g.ExpiresAt > now
            join p in context.Projects.AsNoTracking() on g.TargetProjectId equals p.Id
            where p.IsActive
            select new
            {
                Id = p.Id.Value,
                Name = p.Name.Value,
                Slug = p.Slug.Value
            }
        ).ToListAsync(ct);

        var byProjectId = new Dictionary<Guid, AccessibleProjectResultDto>();
        foreach (var item in memberProjects)
        {
            byProjectId[item.Id] = new AccessibleProjectResultDto(item.Id, item.Name, item.Slug, item.Role);
        }

        foreach (var grant in breakGlassProjects.Where(grant => !byProjectId.ContainsKey(grant.Id)))
        {
            byProjectId[grant.Id] = new AccessibleProjectResultDto(
                grant.Id,
                grant.Name,
                grant.Slug,
                "BreakGlassAdmin");
        }

        return byProjectId.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<ApiKeyResultDto>> GetActiveApiKeysAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var typedProjectId = new ProjectId(projectId);
        var now = timeProvider.GetUtcNow();
        var keys = await context.ApiKeys
            .AsNoTracking()
            .Where(k => k.ProjectId == typedProjectId && k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        return keys
            .Where(k => !k.IsExpiredAt(now))
            .Select(k => new ApiKeyResultDto(
                k.Id.Value,
                k.Name.Value,
                k.CallerApp,
                k.IsActive,
                k.LastUsedAt,
                k.CreatedAt,
                k.Scope.Name,
                k.GetMaskedKey(),
                k.Version))
            .ToList();
    }

    public async Task<UserWorkspaceContextResultDto> ResolveWorkspaceContextAsync(
        User user,
        Guid? requestedProjectId,
        bool throwOnUnauthorized,
        CancellationToken ct)
    {
        var accessibleProjects = await GetAccessibleProjectsAsync(user.Id.Value, isSuperAdmin: false, ct);

        var defaultProject = accessibleProjects.Count > 0 ? accessibleProjects[0] : null;
        var defaultProjectId = defaultProject?.Id;
        const string fallbackRole = "Viewer";
        string defaultRole = defaultProject?.Role ?? fallbackRole;

        if (!requestedProjectId.HasValue || requestedProjectId.Value == Guid.Empty)
        {
            return new UserWorkspaceContextResultDto(
                defaultProjectId,
                defaultRole,
                defaultProjectId,
                accessibleProjects);
        }

        var target = accessibleProjects.FirstOrDefault(p => p.Id == requestedProjectId.Value);
        if (target == null)
        {
            if (throwOnUnauthorized)
            {
                throw new UnauthorizedException("User does not have access to the specified project.");
            }

            return new UserWorkspaceContextResultDto(
                defaultProjectId,
                defaultRole,
                defaultProjectId,
                accessibleProjects);
        }

        return new UserWorkspaceContextResultDto(
            requestedProjectId.Value,
            target.Role,
            defaultProjectId,
            accessibleProjects);
    }
}
