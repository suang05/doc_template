using FluentValidation;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.ListDocumentJobs;

public sealed class ListDocumentJobsQueryValidator : AbstractValidator<ListDocumentJobsQuery>
{
    public ListDocumentJobsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100).WithMessage("Limit must be between 1 and 100.");

        When(x => !string.IsNullOrWhiteSpace(x.Status), () =>
        {
            RuleFor(x => x.Status!)
                .Must(s => Enumeration.GetAll<DocumentJobStatus>().Any(v => v.Name.Equals(s.Trim(), StringComparison.OrdinalIgnoreCase)))
                .WithMessage("Status must be Queued, Running, Succeeded, Failed, or Canceled.");
        });
    }
}
