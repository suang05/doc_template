using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;

public sealed class DeactivateTemplateCommandValidator : AbstractValidator<DeactivateTemplateCommand>
{
    public DeactivateTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("TemplateId is required.");
    }
}
