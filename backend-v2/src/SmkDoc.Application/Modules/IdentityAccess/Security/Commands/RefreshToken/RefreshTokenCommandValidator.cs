using FluentValidation;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("RefreshToken is required.")
            .MaximumLength(256).WithMessage("RefreshToken length must not exceed 256 characters.");
    }
}
