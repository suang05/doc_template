using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;

public sealed class RemoveUserUseCase(
    IUserProjectRoleRepository roleRepo,
    IUnitOfWork uow,
    ICurrentProjectContext projectContext) : IUseCase<RemoveUserCommand>
{
    public async Task ExecuteAsync(RemoveUserCommand command, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Active project context is required to remove a user.");
        }

        var pId = projectContext.ProjectId;
        var uId = new UserId(command.UserId);
        var roleEntry = await roleRepo.GetAsync(pId, uId, ct)
            ?? throw new NotFoundException("User is not a member of this project.");

        if (!string.IsNullOrWhiteSpace(command.IfMatch) && command.IfMatch != "*")
        {
            var currentToken = roleEntry.CreatedAt.ToUnixTimeMilliseconds().ToString();
            var sanitizedMatch = command.IfMatch.Trim('\"', 'W', '/', ' ');
            if (!string.Equals(sanitizedMatch, currentToken, StringComparison.OrdinalIgnoreCase))
            {
                throw new PreconditionFailedException(
                    $"Precondition failed for user role '{command.UserId}'. ETag '{sanitizedMatch}' does not match current state.");
            }
        }

        if (roleEntry.Role == RoleType.Admin)
        {
            var adminCount = await roleRepo.CountAdminsAsync(pId, ct);
            if (adminCount <= 1)
            {
                throw new ConflictException("Cannot remove the last Admin from the project.");
            }
        }

        roleRepo.Remove(roleEntry);
        await uow.CommitAsync(ct);
    }
}
