using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;

/// <summary>
/// Toggles the active status of a user globally. SuperAdmin access required.
/// Triggers automated session revocation via <see cref="Domain.Events.UserDeactivatedDomainEvent"/> upon deactivation.
/// </summary>
public sealed class SetUserStatusUseCase(
    IUserRepository userRepo,
    IUserProjectRoleRepository roleRepo,
    IUnitOfWork uow,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<SetUserStatusCommand, UserResultDto>
{
    public async Task<UserResultDto> ExecuteAsync(SetUserStatusCommand command, CancellationToken ct)
    {
        EnsureSuperAdminAuthorization();

        var uId = new UserId(command.UserId);
        var user = await userRepo.GetByIdAsync(uId, ct)
            ?? throw new NotFoundException("User not found.");

        var now = timeProvider.GetUtcNow();
        await ApplyStatusChangeAsync(user, command.IsActive, now, ct);

        userRepo.Update(user);
        await uow.CommitAsync(ct);

        var roleName = await ResolveRoleNameAsync(uId, ct);
        return ToResultDto(user, roleName);
    }

    private void EnsureSuperAdminAuthorization()
    {
        if (projectContext.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Only global SuperAdmin accounts are authorized to change user account status.");
        }
    }

    private async Task ApplyStatusChangeAsync(User user, bool isActive, DateTimeOffset now, CancellationToken ct)
    {
        if (isActive)
        {
            user.Activate(now);
            return;
        }

        EnsureNotSelfDeactivating(user.Id);
        await EnsureNotLastSuperAdminAsync(user, ct);
        user.Deactivate(now);
    }

    private void EnsureNotSelfDeactivating(UserId targetUserId)
    {
        if (projectContext.UserId.HasValue && projectContext.UserId.Value == targetUserId)
        {
            throw new BusinessRuleViolationException("SuperAdmin cannot deactivate their own account.", "SELF_DEACTIVATION_FORBIDDEN");
        }
    }

    private async Task EnsureNotLastSuperAdminAsync(User user, CancellationToken ct)
    {
        if (user.SystemRole == SystemRole.SuperAdmin && user.IsActive)
        {
            var activeCount = await userRepo.CountActiveSuperAdminsAsync(ct);
            if (activeCount <= 1)
            {
                throw new BusinessRuleViolationException("Cannot deactivate the last active SuperAdmin account.", "LAST_SUPERADMIN_FORBIDDEN");
            }
        }
    }

    private static UserResultDto ToResultDto(User user, string roleName) =>
        new(user.Id.Value,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            roleName,
            user.IsActive,
            user.CreatedAt,
            user.GetConcurrencyToken());

    private async Task<string> ResolveRoleNameAsync(UserId userId, CancellationToken ct)
    {
        if (!projectContext.IsResolved)
        {
            return "Member";
        }

        var role = await roleRepo.GetAsync(projectContext.GetRequiredProjectId(), userId, ct);
        return role != null ? role.Role.Name : "Member";
    }
}
