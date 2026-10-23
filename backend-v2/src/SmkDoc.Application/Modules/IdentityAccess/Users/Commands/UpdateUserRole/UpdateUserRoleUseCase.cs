using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;

public sealed class UpdateUserRoleUseCase(
    IUserProjectRoleRepository roleRepo,
    IUserRepository userRepo,
    IUnitOfWork uow,
    IValidator<UpdateUserRoleCommand> validator) : IUseCase<UpdateUserRoleCommand, UserResultDto>
{
    private readonly IUserProjectRoleRepository _roleRepo = roleRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IUnitOfWork _uow = uow;
    private readonly IValidator<UpdateUserRoleCommand> _validator = validator;

    public async Task<UserResultDto> ExecuteAsync(UpdateUserRoleCommand command, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var pId = new ProjectId(command.ProjectId);
        var uId = new UserId(command.UserId);
        var roleEntry = await _roleRepo.GetAsync(pId, uId, ct)
            ?? throw new NotFoundException("User is not a member of this project.");

        var targetRole = RoleType.FromDisplayName<RoleType>(command.Role);

        // Guard: Cannot demote the last Admin of the project
        if (roleEntry.Role == RoleType.Admin && targetRole != RoleType.Admin)
        {
            var adminCount = await _roleRepo.CountAdminsAsync(pId, ct);
            if (adminCount <= 1)
            {
                throw new ConflictException("Cannot remove the last Admin from the project.");
            }
        }

        roleEntry.UpdateRole(targetRole);
        _roleRepo.Update(roleEntry);
        await _uow.CommitAsync(ct);

        var user = await _userRepo.GetByIdAsync(uId, ct)
            ?? throw new NotFoundException("User not found.");

        return new UserResultDto(
            user.Id.Value,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            roleEntry.Role.Name,
            user.IsActive,
            user.CreatedAt
        );
    }
}
