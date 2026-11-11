using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;

public sealed class UpdateUserRoleUseCase(
    IUserProjectRoleRepository roleRepo,
    IUserRepository userRepo,
    IUnitOfWork uow,
    ICurrentProjectContext projectContext) : IUseCase<UpdateUserRoleCommand, UserResultDto>
{
    public async Task<UserResultDto> ExecuteAsync(UpdateUserRoleCommand command, CancellationToken ct = default)
    {
        var pId = projectContext.ProjectId;
        var uId = new UserId(command.UserId);
        var roleEntry = await roleRepo.GetAsync(pId, uId, ct)
            ?? throw new NotFoundException("User is not a member of this project.");

        var targetRole = RoleType.FromDisplayName<RoleType>(command.Role);
        await EnsureNotDemotingLastAdminAsync(pId, roleEntry, targetRole, ct);

        roleEntry.UpdateRole(targetRole);
        roleRepo.Update(roleEntry);
        await uow.CommitAsync(ct);

        var user = await userRepo.GetByIdAsync(uId, ct)
            ?? throw new NotFoundException("User not found.");

        return ToResultDto(user, roleEntry);
    }

    private async Task EnsureNotDemotingLastAdminAsync(
        ProjectId pId,
        UserProjectRole roleEntry,
        RoleType targetRole,
        CancellationToken ct)
    {
        if (roleEntry.Role == RoleType.Admin && targetRole != RoleType.Admin)
        {
            var adminCount = await roleRepo.CountAdminsAsync(pId, ct);
            if (adminCount <= 1)
            {
                throw new ConflictException("Cannot remove the last Admin from the project.");
            }
        }
    }

    private static UserResultDto ToResultDto(User user, UserProjectRole roleEntry) =>
        new(user.Id.Value,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            roleEntry.Role.Name,
            user.IsActive,
            user.CreatedAt,
            roleEntry.GetConcurrencyToken());
}
