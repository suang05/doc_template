using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;

public sealed class GetCurrentUserProfileUseCase(
    IUserRepository userRepo,
    IUserProjectRoleRepository roleRepo,
    IProjectRepository projectRepo) : IUseCase<GetCurrentUserProfileQuery, CurrentUserProfileResultDto>
{
    public async Task<CurrentUserProfileResultDto> ExecuteAsync(GetCurrentUserProfileQuery query, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(query.UserId, ct);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("User account not found or inactive.");
        }

        var accessibleProjects = new List<AccessibleProjectDto>();

        if (user.SystemRole == SystemRole.SuperAdmin)
        {
            var allProjects = await projectRepo.ListActiveAsync(ct);
            accessibleProjects = allProjects
                .Select(p => new AccessibleProjectDto(p.Id, p.Name, p.Slug, "Admin"))
                .ToList();
        }
        else
        {
            var userRoles = await roleRepo.ListByUserAsync(user.Id, ct);
            if (userRoles.Count > 0)
            {
                var projectIds = userRoles.Select(r => r.ProjectId).ToHashSet();
                var projects = await projectRepo.ListByIdsAsync(projectIds, ct);
                var activeProjects = projects.Where(p => p.IsActive).ToList();
                var roleMap = userRoles.ToDictionary(r => r.ProjectId, r => r.Role.ToString());

                accessibleProjects = activeProjects.Select(p =>
                    new AccessibleProjectDto(p.Id, p.Name, p.Slug, roleMap.GetValueOrDefault(p.Id, "Viewer"))
                ).ToList();
            }
        }

        var defaultProjectId = accessibleProjects.FirstOrDefault()?.Id;

        return new CurrentUserProfileResultDto(
            user.Id,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            user.SystemRole.Name,
            accessibleProjects,
            defaultProjectId
        );
    }
}
