using FluentValidation;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;

public sealed class RemoveUserCommandValidator : AbstractValidator<RemoveUserCommand>
{
    public RemoveUserCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty()
            .WithMessage("ProjectId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty()
            .WithMessage("CurrentUserId is required.");

        RuleFor(x => x)
            .Must(x => x.UserId != x.CurrentUserId)
            .WithMessage("Cannot remove yourself from the project.");
    }
}
