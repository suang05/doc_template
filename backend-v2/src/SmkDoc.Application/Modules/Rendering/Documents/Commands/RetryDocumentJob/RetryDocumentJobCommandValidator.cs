using FluentValidation;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.RetryDocumentJob;

public sealed class RetryDocumentJobCommandValidator : AbstractValidator<RetryDocumentJobCommand>
{
    public RetryDocumentJobCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId is required.");
    }
}
