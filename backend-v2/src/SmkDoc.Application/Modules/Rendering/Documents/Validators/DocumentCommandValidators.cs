using System.Text.Json;
using FluentValidation;
using SmkDoc.Application.DTOs.Documents;

namespace SmkDoc.Application.Validators.Documents;

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
        RuleFor(x => x.Data)
            .Must(BeValidJsonObject)
            .WithMessage("Data payload must be a valid non-empty JSON object.");

        RuleFor(x => x.Output)
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

/// <summary>
/// Validator for <see cref="HtmlToPdfCommand"/>.
/// </summary>
public sealed class HtmlToPdfCommandValidator : AbstractValidator<HtmlToPdfCommand>
{
    public HtmlToPdfCommandValidator()
    {
        RuleFor(x => x.Html)
            .NotEmpty().WithMessage("HTML content cannot be empty.");
    }
}
