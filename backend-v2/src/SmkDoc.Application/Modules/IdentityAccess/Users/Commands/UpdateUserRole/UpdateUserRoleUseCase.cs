using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;

public sealed class UpdateUserRoleUseCase(
    IUserProjectRoleRepository roleRepo,
    IUserRepository userRepo,
    IUnitOfWork uow,
    TimeProvider timeProvider) : IUseCase<UpdateUserRoleCommand, UserResultDto>
{
    public async Task<UserResultDto> ExecuteAsync(UpdateUserRoleCommand command, CancellationToken ct)
    {
        var uId = new UserId(command.UserId);
        var roleEntry = await roleRepo.GetForCurrentProjectAsync(uId, ct)
            ?? throw new NotFoundException("User is not a member of this project.");

        if (roleEntry.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        var targetRole = RoleType.FromDisplayName<RoleType>(command.Role);
        await EnsureNotDemotingLastAdminAsync(roleEntry, targetRole, ct);

        roleEntry.UpdateRole(targetRole, timeProvider.GetUtcNow());
        roleRepo.Update(roleEntry);
        await uow.CommitAsync(ct);

        var user = await userRepo.GetByIdAsync(uId, ct)
            ?? throw new NotFoundException("User not found.");

        return UserResultDto.FromEntity(user, roleEntry.Role.Name, roleEntry.Version);
    }

    private async Task EnsureNotDemotingLastAdminAsync(
        UserProjectRole roleEntry,
        RoleType targetRole,
        CancellationToken ct)
    {
        if (roleEntry.Role == RoleType.Admin && targetRole != RoleType.Admin)
        {
            var adminCount = await roleRepo.CountAdminsForCurrentProjectAsync(ct);
            if (adminCount <= 1)
            {
                throw new ConflictException("Cannot remove the last Admin from the project.");
            }
        }
    }
}
