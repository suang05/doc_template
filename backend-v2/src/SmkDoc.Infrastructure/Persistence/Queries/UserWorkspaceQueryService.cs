using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

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
            var projects = await context.Projects
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);

            return projects
                .Select(p => new AccessibleProjectDto(p.Id.Value, p.Name.Value, p.Slug.Value, "Admin"))
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
            .Select(x => new AccessibleProjectDto(x.Id.Value, x.Name.Value, x.Slug.Value, x.Role))
            .ToList();
    }

    public async Task<IReadOnlyList<ApiKeyDto>> GetActiveApiKeysAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var typedProjectId = new ProjectId(projectId);
        var keys = await context.ApiKeys
            .AsNoTracking()
            .Where(k => k.ProjectId == typedProjectId && k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        return keys
            .Select(k => new ApiKeyDto(
                k.Id.Value,
                k.Name.Value,
                k.CallerApp,
                k.IsActive,
                k.LastUsedAt,
                k.CreatedAt,
                k.Scope.Name))
            .ToList();
    }

    public async Task<UserWorkspaceContextDto> ResolveWorkspaceContextAsync(
        User user,
        Guid? requestedProjectId,
        bool throwOnUnauthorized = true,
        CancellationToken ct = default)
    {
        var isSuperAdmin = user.SystemRole == SystemRole.SuperAdmin;
        var accessibleProjects = await GetAccessibleProjectsAsync(user.Id.Value, isSuperAdmin, ct);

        var defaultProject = accessibleProjects.FirstOrDefault();
        var defaultProjectId = defaultProject?.Id;
        string activeRole = isSuperAdmin ? "Admin" : "Viewer";
        Guid? activeProjectId = null;

        if (requestedProjectId.HasValue && requestedProjectId.Value != Guid.Empty)
        {
            var target = accessibleProjects.FirstOrDefault(p => p.Id == requestedProjectId.Value);
            if (target == null && !isSuperAdmin)
            {
                if (throwOnUnauthorized)
                {
                    throw new UnauthorizedException("User does not have access to the specified project.");
                }

                activeProjectId = defaultProjectId;
                if (defaultProject != null)
                {
                    activeRole = defaultProject.Role;
                }
            }
            else
            {
                activeProjectId = requestedProjectId.Value;
                if (target != null)
                {
                    activeRole = target.Role;
                }
            }
        }
        else
        {
            activeProjectId = defaultProjectId;
            if (defaultProject != null)
            {
                activeRole = defaultProject.Role;
            }
        }

        return new UserWorkspaceContextDto(
            activeProjectId,
            activeRole,
            defaultProjectId,
            accessibleProjects);
    }
}
