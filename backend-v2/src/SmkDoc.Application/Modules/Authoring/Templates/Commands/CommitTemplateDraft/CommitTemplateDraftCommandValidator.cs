using System.Text.RegularExpressions;
using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

public sealed partial class CommitTemplateDraftCommandValidator : AbstractValidator<CommitTemplateDraftCommand>
{
    public CommitTemplateDraftCommandValidator()
    {
        RuleFor(x => x.DraftId)
            .NotEmpty().WithMessage("DraftId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MinimumLength(2).WithMessage("Template name must be at least 2 characters.")
            .MaximumLength(150).WithMessage("Template name cannot exceed 150 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Template slug is required.")
            .MaximumLength(100).WithMessage("Template slug cannot exceed 100 characters.")
            .Matches(SlugRegex())
            .WithMessage("Template slug must only contain lowercase alphanumeric characters and hyphens.");

        RuleFor(x => x.Category)
            .MaximumLength(50).WithMessage("Category cannot exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.Category));

        RuleFor(x => x.Mappings)
            .NotNull().WithMessage("Mappings collection is required.");
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}
