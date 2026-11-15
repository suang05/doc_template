using FluentValidation;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.CreateDocumentJob;

public sealed class CreateDocumentJobCommandValidator : AbstractValidator<CreateDocumentJobCommand>
{
    public CreateDocumentJobCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Template slug is required.")
            .MaximumLength(DocumentJob.MaxTemplateSlugLength)
            .WithMessage($"Template slug must not exceed {DocumentJob.MaxTemplateSlugLength} characters.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one document render item is required.");

        RuleFor(x => x.OutputFormat)
            .NotEmpty().WithMessage("Output format is required.")
            .Must(f => f.Equals("pdf", StringComparison.OrdinalIgnoreCase) ||
                       f.Equals("docx", StringComparison.OrdinalIgnoreCase) ||
                       f.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Output format must be 'pdf', 'docx', or 'xlsx'.");

        RuleFor(x => x.MaxAttempts)
            .InclusiveBetween(1, 10).WithMessage("MaxAttempts must be between 1 and 10.");

        When(x => !string.IsNullOrWhiteSpace(x.WebhookUrl), () =>
        {
            RuleFor(x => x.WebhookUrl!)
                .MaximumLength(DocumentJob.MaxWebhookUrlLength)
                .WithMessage($"WebhookUrl cannot exceed {DocumentJob.MaxWebhookUrlLength} characters.")
                .Must(IsValidHttpOrHttpsUri)
                .WithMessage("WebhookUrl must be an absolute http:// or https:// URI.");
        });
    }

    private static bool IsValidHttpOrHttpsUri(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
