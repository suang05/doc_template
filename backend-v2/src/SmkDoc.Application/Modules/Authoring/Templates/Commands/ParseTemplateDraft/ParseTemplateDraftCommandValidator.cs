using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;

public sealed class ParseTemplateDraftCommandValidator : AbstractValidator<ParseTemplateDraftCommand>
{
    public ParseTemplateDraftCommandValidator()
    {
        RuleFor(x => x.FileStream)
            .NotNull().WithMessage("Template file stream is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .MaximumLength(255).WithMessage("File name cannot exceed 255 characters.");
    }
}
