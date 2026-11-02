using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;

public sealed class SaveTemplateMappingsCommandValidator : AbstractValidator<SaveTemplateMappingsCommand>
{
    public SaveTemplateMappingsCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("TemplateId is required.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Field mapping items collection is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Placeholder)
                .NotEmpty().WithMessage("Placeholder is required.")
                .MaximumLength(150).WithMessage("Placeholder cannot exceed 150 characters.");
        });
    }
}
