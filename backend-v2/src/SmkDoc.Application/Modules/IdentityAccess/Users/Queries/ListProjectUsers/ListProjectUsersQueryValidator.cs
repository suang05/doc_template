using FluentValidation;

namespace SmkDoc.Application.UseCases.Users.Queries.ListProjectUsers;

public sealed class ListProjectUsersQueryValidator : AbstractValidator<ListProjectUsersQuery>
{
    public ListProjectUsersQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty()
            .WithMessage("ProjectId is required.");
    }
}
