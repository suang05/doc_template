using FluentValidation;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;

/// <summary>
/// Validator for <see cref="RenderStatelessDocumentCommand"/>.
/// </summary>
public sealed class RenderStatelessDocumentCommandValidator : AbstractValidator<RenderStatelessDocumentCommand>
{
    public RenderStatelessDocumentCommandValidator()
    {
        RuleFor(x => x.FileStream)
            .NotNull().WithMessage("Template file stream is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .MaximumLength(255).WithMessage("File name cannot exceed 255 characters.");
    }
}
