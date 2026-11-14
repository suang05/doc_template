using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class UserWorkspaceQueryService(AppDbContext context) : IUserWorkspaceQueryService
{
    public async Task<IReadOnlyList<AccessibleProjectResultDto>> GetAccessibleProjectsAsync(
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct)
    {
        if (isSuperAdmin)
        {
            var projects = await context.Projects
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);

            return projects
                .Select(p => new AccessibleProjectResultDto(p.Id.Value, p.Name.Value, p.Slug.Value, "Admin"))
                .ToList();
        }

        var typedUserId = new UserId(userId);
        var results = await (
            from r in context.UserProjectRoles.AsNoTracking()
            where r.UserId == typedUserId
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
            .Select(x => new AccessibleProjectResultDto(x.Id.Value, x.Name.Value, x.Slug.Value, x.Role))
            .ToList();
    }

    public async Task<IReadOnlyList<ApiKeyResultDto>> GetActiveApiKeysAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var typedProjectId = new ProjectId(projectId);
        var keys = await context.ApiKeys
            .AsNoTracking()
            .Where(k => k.ProjectId == typedProjectId && k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        return keys
            .Select(k => new ApiKeyResultDto(
                k.Id.Value,
                k.Name.Value,
                k.CallerApp,
                k.IsActive,
                k.LastUsedAt,
                k.CreatedAt,
                k.Scope.Name,
                ConcurrencyToken: (k.UpdatedAt ?? k.CreatedAt).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)))
            .ToList();
    }

    public async Task<UserWorkspaceContextResultDto> ResolveWorkspaceContextAsync(
        User user,
        Guid? requestedProjectId,
        bool throwOnUnauthorized,
        CancellationToken ct)
    {
        var isSuperAdmin = user.SystemRole == SystemRole.SuperAdmin;
        var accessibleProjects = await GetAccessibleProjectsAsync(user.Id.Value, isSuperAdmin, ct);

        var defaultProject = accessibleProjects.Count > 0 ? accessibleProjects[0] : null;
        var defaultProjectId = defaultProject?.Id;
        string fallbackRole = isSuperAdmin ? "Admin" : "Viewer";
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
        if (target == null && !isSuperAdmin)
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
            target?.Role ?? fallbackRole,
            defaultProjectId,
            accessibleProjects);
    }
}
