using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;

public sealed class SaveTemplateDatasetsCommandValidator : AbstractValidator<SaveTemplateDatasetsCommand>
{
    public SaveTemplateDatasetsCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("TemplateId is required.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Dataset items collection is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.DatasetId)
                .NotEmpty().WithMessage("DatasetId is required.");

            item.RuleFor(i => i.Alias)
                .NotEmpty().WithMessage("Dataset alias is required.")
                .MaximumLength(50).WithMessage("Dataset alias cannot exceed 50 characters.");
        });
    }
}
