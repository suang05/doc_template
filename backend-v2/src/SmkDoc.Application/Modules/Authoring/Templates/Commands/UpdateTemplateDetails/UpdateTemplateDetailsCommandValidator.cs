using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;

public sealed class UpdateTemplateDetailsCommandValidator : AbstractValidator<UpdateTemplateDetailsCommand>
{
    public UpdateTemplateDetailsCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("TemplateId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MinimumLength(2).WithMessage("Template name must be at least 2 characters.")
            .MaximumLength(150).WithMessage("Template name cannot exceed 150 characters.");

        RuleFor(x => x.Category)
            .MaximumLength(50).WithMessage("Category cannot exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.Category));
    }
}
