using FluentValidation;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

public sealed class ListProjectUsersQueryValidator : AbstractValidator<ListProjectUsersQuery>
{
    public ListProjectUsersQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty()
            .WithMessage("ProjectId is required.");
    }
}
