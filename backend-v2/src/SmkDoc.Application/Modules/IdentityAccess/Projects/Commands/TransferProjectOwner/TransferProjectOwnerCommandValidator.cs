using FluentValidation;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.TransferProjectOwner;

public sealed class TransferProjectOwnerCommandValidator : AbstractValidator<TransferProjectOwnerCommand>
{
    public TransferProjectOwnerCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is required.");

        RuleFor(x => x.NewOwnerUserId)
            .NotEmpty().WithMessage("NewOwnerUserId is required.");
    }
}

