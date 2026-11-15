using FluentValidation;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

public sealed class ListGenerationLogsQueryValidator : AbstractValidator<ListGenerationLogsQuery>
{
    public ListGenerationLogsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit must be between 1 and 100.");
    }
}
