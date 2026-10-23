using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;

public sealed class SetUserStatusUseCase(
    IUserRepository userRepo,
    IUserProjectRoleRepository roleRepo,
    IUnitOfWork uow,
    IValidator<SetUserStatusCommand> validator,
    TimeProvider? timeProvider = null) : IUseCase<SetUserStatusCommand, UserResultDto>
{
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IUserProjectRoleRepository _roleRepo = roleRepo;
    private readonly IUnitOfWork _uow = uow;
    private readonly IValidator<SetUserStatusCommand> _validator = validator;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<UserResultDto> ExecuteAsync(SetUserStatusCommand command, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var pId = new ProjectId(command.ProjectId);
        var uId = new UserId(command.UserId);
        var hasRole = await _roleRepo.GetAsync(pId, uId, ct);
        if (hasRole == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        var user = await _userRepo.GetByIdAsync(uId, ct)
            ?? throw new NotFoundException("User not found.");

        var now = _timeProvider.GetUtcNow();
        if (command.IsActive)
        {
            user.Activate(now);
        }
        else
        {
            user.Deactivate(now);
        }

        _userRepo.Update(user);
        await _uow.CommitAsync(ct);

        return new UserResultDto(
            user.Id.Value,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            hasRole.Role.Name,
            user.IsActive,
            user.CreatedAt
        );
    }
}
