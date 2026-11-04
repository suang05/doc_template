using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;

public sealed class InviteUserUseCase(
    IUserRepository userRepo,
    IUserProjectRoleRepository roleRepo,
    IPasswordHasher passwordHasher,
    IUnitOfWork uow,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<InviteUserCommand, UserResultDto>
{
    public async Task<UserResultDto> ExecuteAsync(InviteUserCommand command, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Active project context is required to invite a user.");
        }

        var roleType = Enumeration.FromDisplayName<RoleType>(command.Role);
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var now = timeProvider.GetUtcNow();
        var pId = projectContext.ProjectId;

        var existingUser = await userRepo.GetByEmailAsync(normalizedEmail, ct);
        if (existingUser != null)
        {
            var existingRole = await roleRepo.GetAsync(pId, existingUser.Id, ct);
            if (existingRole != null)
            {
                throw new ConflictException($"User '{normalizedEmail}' is already a member of this project.");
            }

            var newRole = UserProjectRole.Create(existingUser.Id, pId, roleType, now);
            await roleRepo.AddAsync(newRole, ct);
            await uow.CommitAsync(ct);

            return new UserResultDto(
                existingUser.Id.Value,
                existingUser.Email,
                existingUser.FirstName,
                existingUser.LastName,
                roleType.Name,
                existingUser.IsActive,
                existingUser.CreatedAt
            );
        }

        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8)
        {
            throw new ValidationException("Password", "Password must be at least 8 characters.");
        }

        var newUser = User.Register(
            EmailAddress.Create(normalizedEmail),
            passwordHasher.HashPassword(command.Password),
            command.FirstName,
            command.LastName,
            now
        );

        await userRepo.AddAsync(newUser, ct);
        await roleRepo.AddAsync(UserProjectRole.Create(newUser.Id, pId, roleType, now), ct);
        await uow.CommitAsync(ct);

        return new UserResultDto(
            newUser.Id.Value,
            newUser.Email,
            newUser.FirstName,
            newUser.LastName,
            roleType.Name,
            newUser.IsActive,
            newUser.CreatedAt
        );
    }
}
