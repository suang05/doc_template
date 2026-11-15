using FluentValidation;
using SmkDoc.Domain.TenantManagement;

namespace SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.GrantBreakGlassAccess;

public sealed class GrantBreakGlassAccessCommandValidator : AbstractValidator<GrantBreakGlassAccessCommand>
{
    public GrantBreakGlassAccessCommandValidator()
    {
        RuleFor(x => x.TargetProjectId)
            .NotEmpty().WithMessage("TargetProjectId is required.");

        RuleFor(x => x.SuperAdminUserId)
            .NotEmpty().WithMessage("SuperAdminUserId is required.");

        RuleFor(x => x.GrantedByUserId)
            .NotEmpty().WithMessage("GrantedByUserId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Break-glass justification reason is required.")
            .MaximumLength(BreakGlassGrant.MaxReasonLength)
            .WithMessage($"Reason must not exceed {BreakGlassGrant.MaxReasonLength} characters.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, (int)BreakGlassGrant.DefaultMaxDuration.TotalMinutes)
            .WithMessage($"DurationMinutes must be between 1 and {(int)BreakGlassGrant.DefaultMaxDuration.TotalMinutes}.");
    }
}
