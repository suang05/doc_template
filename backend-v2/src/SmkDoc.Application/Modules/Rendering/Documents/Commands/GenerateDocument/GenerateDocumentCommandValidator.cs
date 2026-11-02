using System.Text.Json;
using FluentValidation;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;

/// <summary>
/// Validator for <see cref="GenerateDocumentCommand"/>.
/// </summary>
public sealed class GenerateDocumentCommandValidator : AbstractValidator<GenerateDocumentCommand>
{
    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "docx", "xlsx"
    };

    public GenerateDocumentCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Template slug is required.")
            .MaximumLength(100).WithMessage("Template slug cannot exceed 100 characters.");

        RuleFor(x => x.Payload)
            .Must(BeValidJsonObject)
            .WithMessage("Data payload must be a valid non-empty JSON object.");

        RuleFor(x => x.OutputFormat)
            .NotEmpty().WithMessage("Output format is required.")
            .Must(x => AllowedFormats.Contains(x))
            .WithMessage("Output format must be one of: 'pdf', 'docx', 'xlsx'.");

        RuleFor(x => x.DocumentRef)
            .MaximumLength(100).WithMessage("Document reference cannot exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.DocumentRef));

        RuleFor(x => x.ChangeNote)
            .MaximumLength(500).WithMessage("Change note cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.ChangeNote));
    }

    private static bool BeValidJsonObject(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object;
    }
}
