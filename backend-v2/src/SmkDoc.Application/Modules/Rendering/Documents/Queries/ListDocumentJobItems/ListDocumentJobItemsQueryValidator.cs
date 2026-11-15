using FluentValidation;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.ListDocumentJobItems;

public sealed class ListDocumentJobItemsQueryValidator : AbstractValidator<ListDocumentJobItemsQuery>
{
    public ListDocumentJobItemsQueryValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId is required.");
    }
}
