using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;

public sealed class RollbackTemplateVersionCommandValidator : AbstractValidator<RollbackTemplateVersionCommand>
{
    public RollbackTemplateVersionCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("TemplateId is required.");

        RuleFor(x => x.TargetVersion)
            .GreaterThan(0).WithMessage("TargetVersion must be greater than zero.");
    }
}
