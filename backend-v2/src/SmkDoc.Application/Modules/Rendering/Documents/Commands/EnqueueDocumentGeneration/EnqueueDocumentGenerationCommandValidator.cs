using FluentValidation;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;

public sealed class EnqueueDocumentGenerationCommandValidator : AbstractValidator<EnqueueDocumentGenerationCommand>
{
    public EnqueueDocumentGenerationCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Template slug is required.")
            .MaximumLength(100).WithMessage("Template slug must not exceed 100 characters.");

        RuleFor(x => x.OutputFormat)
            .NotEmpty().WithMessage("Output format is required.")
            .Must(f => f.Equals("pdf", StringComparison.OrdinalIgnoreCase) ||
                       f.Equals("docx", StringComparison.OrdinalIgnoreCase) ||
                       f.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Output format must be 'pdf', 'docx', or 'xlsx'.");
    }
}

