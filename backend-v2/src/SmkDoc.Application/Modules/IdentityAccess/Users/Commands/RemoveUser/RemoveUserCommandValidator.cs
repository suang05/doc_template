using FluentValidation;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;

public sealed class RemoveUserCommandValidator : AbstractValidator<RemoveUserCommand>
{
    public RemoveUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x)
            .Must(x => x.UserId != x.CurrentUserId)
            .WithMessage("Users cannot remove themselves from a project.");
    }
}
