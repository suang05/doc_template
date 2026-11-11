using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
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
        var user = await EnsureActiveUserAsync(new UserId(query.UserId), ct);
        var activeProjectId = projectContext.IsResolved ? projectContext.ProjectId.Value : (Guid?)null;
        var workspace = await workspaceQueryService.ResolveWorkspaceContextAsync(user, activeProjectId, throwOnUnauthorized: false, ct);

        return ToResultDto(user, workspace);
    }

    private async Task<User> EnsureActiveUserAsync(UserId userId, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(userId, ct);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("User account not found or inactive.");
        }
        return user;
    }

    private static CurrentUserProfileResultDto ToResultDto(User user, UserWorkspaceContextResultDto workspace) =>
        new(user.Id.Value,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            user.SystemRole.Name,
            workspace.AccessibleProjects,
            workspace.DefaultProjectId,
            workspace.ActiveProjectId);
}
