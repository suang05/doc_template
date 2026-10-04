using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;

public sealed class InviteUserUseCase(
    IUserRepository userRepo,
    IUserProjectRoleRepository roleRepo,
    IPasswordHasher passwordHasher,
    IUnitOfWork uow,
    IValidator<InviteUserCommand> validator) : IUseCase<InviteUserCommand, UserResponseDto>
{
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IUserProjectRoleRepository _roleRepo = roleRepo;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IUnitOfWork _uow = uow;
    private readonly IValidator<InviteUserCommand> _validator = validator;

    public async Task<UserResponseDto> ExecuteAsync(InviteUserCommand command, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var roleType = Enumeration.FromDisplayName<RoleType>(command.Role);
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        var existingUser = await _userRepo.GetByEmailAsync(normalizedEmail, ct);
        if (existingUser != null)
        {
            var existingRole = await _roleRepo.GetAsync(command.ProjectId, existingUser.Id, ct);
            if (existingRole != null)
            {
                throw new ConflictException($"User '{normalizedEmail}' is already a member of this project.");
            }

            var newRole = new UserProjectRole(existingUser.Id, command.ProjectId, roleType);
            await _roleRepo.AddAsync(newRole, ct);
            await _uow.CommitAsync(ct);

            return new UserResponseDto(
                existingUser.Id,
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
            throw new ValidationException("Password",
                "Password must be at least 8 characters.");
        }

        var newUser = new User(
            normalizedEmail,
            _passwordHasher.HashPassword(command.Password),
            command.FirstName,
            command.LastName
        );

        await _userRepo.AddAsync(newUser, ct);
        await _roleRepo.AddAsync(new UserProjectRole(newUser.Id, command.ProjectId, roleType), ct);
        await _uow.CommitAsync(ct);

        return new UserResponseDto(
            newUser.Id,
            newUser.Email,
            newUser.FirstName,
            newUser.LastName,
            roleType.Name,
            newUser.IsActive,
            newUser.CreatedAt
        );
    }
}
