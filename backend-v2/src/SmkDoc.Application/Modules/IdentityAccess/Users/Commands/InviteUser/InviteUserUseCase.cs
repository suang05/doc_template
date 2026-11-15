using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.Services;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;

/// <summary>
/// Invites an existing or new user to the active project context and assigns their tenant role.
/// </summary>
public sealed class InviteUserUseCase(
    IUserAccountProcessor userAccountProcessor,
    IUserProjectRoleRepository roleRepo,
    IUnitOfWork uow,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<InviteUserCommand, UserResultDto>
{
    public async Task<UserResultDto> ExecuteAsync(InviteUserCommand command, CancellationToken ct)
    {
        var pId = projectContext.GetRequiredProjectId();
        var roleType = Enumeration.FromDisplayName<RoleType>(command.Role);
        var now = timeProvider.GetUtcNow();

        var user = await userAccountProcessor.GetOrRegisterUserAsync(command.Email, command.Password, command.FirstName, command.LastName, now, ct);
        await EnsureNotMemberAsync(pId, user, ct);

        var role = await AssignProjectRoleAsync(pId, user.Id, roleType, now, ct);
        await uow.CommitAsync(ct);

        return UserResultDto.FromEntity(user, roleType.Name, role.Version);
    }

    private async Task EnsureNotMemberAsync(Domain.ValueObjects.Ids.ProjectId pId, User user, CancellationToken ct)
    {
        var existingRole = await roleRepo.GetAsync(pId, user.Id, ct);
        if (existingRole != null)
        {
            throw new ConflictException($"User '{user.Email.Value}' is already a member of this project.");
        }
    }

    private async Task<UserProjectRole> AssignProjectRoleAsync(
        Domain.ValueObjects.Ids.ProjectId pId,
        Domain.ValueObjects.Ids.UserId userId,
        RoleType roleType,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var newRole = UserProjectRole.Create(userId, pId, roleType, now);
        await roleRepo.AddAsync(newRole, ct);
        return newRole;
    }
}
