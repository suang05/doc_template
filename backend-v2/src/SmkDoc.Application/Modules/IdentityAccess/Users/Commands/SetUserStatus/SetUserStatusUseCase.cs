using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;

public sealed class SetUserStatusUseCase(
    IUserRepository userRepo,
    IUserProjectRoleRepository roleRepo,
    IRefreshTokenRepository refreshTokenRepo,
    IUnitOfWork uow,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<SetUserStatusCommand, UserResultDto>
{
    public async Task<UserResultDto> ExecuteAsync(SetUserStatusCommand command, CancellationToken ct = default)
    {
        if (projectContext.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Only global SuperAdmin accounts are authorized to change user account status.");
        }

        var uId = new UserId(command.UserId);
        var user = await userRepo.GetByIdAsync(uId, ct)
            ?? throw new NotFoundException("User not found.");

        var now = timeProvider.GetUtcNow();
        if (command.IsActive)
        {
            user.Activate(now);
        }
        else
        {
            if (projectContext.UserId.HasValue && projectContext.UserId.Value == uId)
            {
                throw new BusinessRuleViolationException(
                    "SuperAdmin cannot deactivate their own account.",
                    "SELF_DEACTIVATION_FORBIDDEN");
            }

            if (user.SystemRole == SystemRole.SuperAdmin && user.IsActive)
            {
                var activeSuperAdminCount = await userRepo.CountActiveSuperAdminsAsync(ct);
                if (activeSuperAdminCount <= 1)
                {
                    throw new BusinessRuleViolationException(
                        "Cannot deactivate the last active SuperAdmin account.",
                        "LAST_SUPERADMIN_FORBIDDEN");
                }
            }

            user.Deactivate(now);
            await refreshTokenRepo.RevokeAllByUserIdAsync(uId, now, ct);
        }

        userRepo.Update(user);
        await uow.CommitAsync(ct);

        var roleName = "Member";
        if (projectContext.IsResolved)
        {
            var hasRole = await roleRepo.GetAsync(projectContext.ProjectId, uId, ct);
            if (hasRole != null)
            {
                roleName = hasRole.Role.Name;
            }
        }

        return new UserResultDto(
            user.Id.Value,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            roleName,
            user.IsActive,
            user.CreatedAt,
            (user.UpdatedAt ?? user.CreatedAt).ToUnixTimeMilliseconds().ToString()
        );
    }
}
