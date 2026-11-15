using FluentValidation;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.CancelDocumentJob;

public sealed class CancelDocumentJobCommandValidator : AbstractValidator<CancelDocumentJobCommand>
{
    public CancelDocumentJobCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId is required.");
    }
}
