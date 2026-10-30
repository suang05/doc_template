using FluentValidation;
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
    IValidator<SetUserStatusCommand> validator,
    ICurrentProjectContext projectContext,
    TimeProvider? timeProvider = null) : IUseCase<SetUserStatusCommand, UserResultDto>
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<UserResultDto> ExecuteAsync(SetUserStatusCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        if (projectContext.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Only global SuperAdmin accounts are authorized to change user account status.");
        }

        var pId = new ProjectId(command.ProjectId);
        var uId = new UserId(command.UserId);
        var hasRole = await roleRepo.GetAsync(pId, uId, ct)
            ?? throw new NotFoundException("User is not a member of this project.");

        var user = await userRepo.GetByIdAsync(uId, ct)
            ?? throw new NotFoundException("User not found.");

        var now = this.timeProvider.GetUtcNow();
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

        return new UserResultDto(
            user.Id.Value,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            hasRole.Role.Name,
            user.IsActive,
            user.CreatedAt
        );
    }
}
