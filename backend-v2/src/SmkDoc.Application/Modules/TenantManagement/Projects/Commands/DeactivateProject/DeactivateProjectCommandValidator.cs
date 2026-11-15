using FluentValidation;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.Commands.DeactivateProject;

public sealed class DeactivateProjectCommandValidator : AbstractValidator<DeactivateProjectCommand>
{
    public DeactivateProjectCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is required.");
    }
}
