using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

public sealed class ListTemplatesQueryValidator : AbstractValidator<ListTemplatesQuery>
{
    public ListTemplatesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit must be between 1 and 100.");
    }
}
