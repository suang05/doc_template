using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;

public sealed class SetUserStatusUseCase(
    IUserRepository userRepo,
    IUserProjectRoleRepository roleRepo,
    IUnitOfWork uow,
    IValidator<SetUserStatusCommand> validator) : IUseCase<SetUserStatusCommand>
{
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IUserProjectRoleRepository _roleRepo = roleRepo;
    private readonly IUnitOfWork _uow = uow;
    private readonly IValidator<SetUserStatusCommand> _validator = validator;

    public async Task ExecuteAsync(SetUserStatusCommand command, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new SmkDoc.Domain.Exceptions.ValidationException(validationResult.ToDictionary());
        }

        var hasRole = await _roleRepo.GetAsync(command.ProjectId, command.UserId, ct);
        if (hasRole == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        var user = await _userRepo.GetByIdAsync(command.UserId, ct)
            ?? throw new NotFoundException("User not found.");

        if (command.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        _userRepo.Update(user);
        await _uow.CommitAsync(ct);
    }
}
