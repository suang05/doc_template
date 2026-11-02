using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;

public sealed class SaveTemplateHtmlCommandValidator : AbstractValidator<SaveTemplateHtmlCommand>
{
    public SaveTemplateHtmlCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("TemplateId is required.");

        RuleFor(x => x.Html)
            .NotEmpty().WithMessage("HTML template content cannot be empty.");

        RuleFor(x => x.ChangeNote)
            .MaximumLength(500).WithMessage("Change note cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.ChangeNote));
    }
}
