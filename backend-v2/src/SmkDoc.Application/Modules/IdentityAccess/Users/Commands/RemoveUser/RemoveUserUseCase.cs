using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;

public sealed class RemoveUserUseCase(
    IUserProjectRoleRepository roleRepo,
    IUnitOfWork uow,
    IValidator<RemoveUserCommand> validator) : IUseCase<RemoveUserCommand>
{
    private readonly IUserProjectRoleRepository _roleRepo = roleRepo;
    private readonly IUnitOfWork _uow = uow;
    private readonly IValidator<RemoveUserCommand> _validator = validator;

    public async Task ExecuteAsync(RemoveUserCommand command, CancellationToken ct = default)
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

        if (roleEntry.Role == RoleType.Admin)
        {
            var adminCount = await _roleRepo.CountAdminsAsync(pId, ct);
            if (adminCount <= 1)
            {
                throw new ConflictException("Cannot remove the last Admin from the project.");
            }
        }

        _roleRepo.Remove(roleEntry);
        await _uow.CommitAsync(ct);
    }
}
