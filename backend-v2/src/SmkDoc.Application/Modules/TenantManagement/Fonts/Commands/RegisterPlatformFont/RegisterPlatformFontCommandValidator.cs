using FluentValidation;

namespace SmkDoc.Application.Modules.TenantManagement.Fonts.Commands.RegisterPlatformFont;

public sealed class RegisterPlatformFontCommandValidator : AbstractValidator<RegisterPlatformFontCommand>
{
    public RegisterPlatformFontCommandValidator()
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
