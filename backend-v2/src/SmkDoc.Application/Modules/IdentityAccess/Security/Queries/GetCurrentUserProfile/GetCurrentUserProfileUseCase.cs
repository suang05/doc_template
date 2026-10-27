using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;

public sealed class GetCurrentUserProfileUseCase(
    IUserRepository userRepo,
    IUserWorkspaceQueryService workspaceQueryService,
    ICurrentProjectContext projectContext) : IUseCase<GetCurrentUserProfileQuery, CurrentUserProfileResultDto>
{
    public async Task<CurrentUserProfileResultDto> ExecuteAsync(GetCurrentUserProfileQuery query, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(new UserId(query.UserId), ct);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("User account not found or inactive.");
        }

        var workspace = await workspaceQueryService.ResolveWorkspaceContextAsync(
            user,
            projectContext.IsResolved ? projectContext.ProjectId.Value : (Guid?)null,
            throwOnUnauthorized: false,
            ct);

        return new CurrentUserProfileResultDto(
            user.Id.Value,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            user.SystemRole.Name,
            workspace.AccessibleProjects,
            workspace.DefaultProjectId,
            workspace.ActiveProjectId
        );
    }
}
