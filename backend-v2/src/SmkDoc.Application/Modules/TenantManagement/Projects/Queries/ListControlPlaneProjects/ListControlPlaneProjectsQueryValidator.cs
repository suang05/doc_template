using FluentValidation;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.Queries.ListControlPlaneProjects;

public sealed class ListControlPlaneProjectsQueryValidator : AbstractValidator<ListControlPlaneProjectsQuery>
{
    public ListControlPlaneProjectsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit must be between 1 and 100.");
    }
}
