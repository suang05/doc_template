using FluentValidation;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.Commands.ActivateProject;

public sealed class ActivateProjectCommandValidator : AbstractValidator<ActivateProjectCommand>
{
    public ActivateProjectCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is required.");
    }
}
