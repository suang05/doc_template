using System.Text.RegularExpressions;
using FluentValidation;
using SmkDoc.Application.DTOs.Templates;

namespace SmkDoc.Application.Validators.Templates;

/// <summary>
/// Validator for <see cref="CreateTemplateCommand"/>.
/// </summary>
public sealed partial class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MinimumLength(2).WithMessage("Template name must be at least 2 characters.")
            .MaximumLength(150).WithMessage("Template name cannot exceed 150 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Template slug is required.")
            .MaximumLength(100).WithMessage("Template slug cannot exceed 100 characters.")
            .Matches(SlugRegex())
            .WithMessage("Template slug must only contain lowercase alphanumeric characters, numbers, and hyphens (e.g. 'sales-receipt-v1').");

        RuleFor(x => x.Category)
            .MaximumLength(50).WithMessage("Category cannot exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.Category));
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}

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
