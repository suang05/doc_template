using FluentValidation;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.Validators;

/// <summary>
/// Validator for <see cref="UpdateTemplateMetadataCommand"/>.
/// </summary>
public sealed class UpdateTemplateMetadataCommandValidator : AbstractValidator<UpdateTemplateMetadataCommand>
{
    public UpdateTemplateMetadataCommandValidator()
    {
        RuleFor(x => x.Name)
            .MinimumLength(2).WithMessage("Template name must be at least 2 characters.")
            .MaximumLength(150).WithMessage("Template name cannot exceed 150 characters.")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Category)
            .MaximumLength(50).WithMessage("Category cannot exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.Category));
    }
}
