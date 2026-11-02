using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;

public sealed class UploadFontCommandValidator : AbstractValidator<UploadFontCommand>
{
    public UploadFontCommandValidator()
    {
        RuleFor(x => x.Data)
            .NotNull().WithMessage("Font file stream is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Font file name is required.")
            .MaximumLength(255).WithMessage("Font file name cannot exceed 255 characters.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Font content type is required.");
    }
}
