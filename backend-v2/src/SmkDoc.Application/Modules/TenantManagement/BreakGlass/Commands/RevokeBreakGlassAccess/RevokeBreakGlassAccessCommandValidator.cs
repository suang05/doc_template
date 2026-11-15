using FluentValidation;

namespace SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.RevokeBreakGlassAccess;

public sealed class RevokeBreakGlassAccessCommandValidator : AbstractValidator<RevokeBreakGlassAccessCommand>
{
    public RevokeBreakGlassAccessCommandValidator()
    {
        RuleFor(x => x.GrantId)
            .NotEmpty().WithMessage("GrantId is required.");
    }
}
