using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;

public sealed class GetCurrentUserProfileUseCase(
    IUserRepository userRepo,
    IUserWorkspaceQueryService workspaceQueryService,
    IExecutionContext executionContext) : IUseCase<GetCurrentUserProfileQuery, CurrentUserProfileResultDto>
{
    public async Task<CurrentUserProfileResultDto> ExecuteAsync(GetCurrentUserProfileQuery query, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(query.UserId, ct);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("User account not found or inactive.");
        }

        var isSuperAdmin = user.SystemRole == SystemRole.SuperAdmin;
        var accessibleProjects = await workspaceQueryService.GetAccessibleProjectsAsync(
            user.Id,
            isSuperAdmin,
            ct);

        var defaultProjectId = accessibleProjects.FirstOrDefault()?.Id;

        Guid? activeProjectId = null;
        if (executionContext.ProjectId.HasValue)
        {
            var requestedId = executionContext.ProjectId.Value;
            var isAccessible = isSuperAdmin || accessibleProjects.Any(p => p.Id == requestedId);
            if (isAccessible)
            {
                activeProjectId = requestedId;
            }
        }

        activeProjectId ??= defaultProjectId;

        return new CurrentUserProfileResultDto(
            user.Id,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            user.SystemRole.Name,
            accessibleProjects,
            defaultProjectId,
            activeProjectId
        );
    }
}
