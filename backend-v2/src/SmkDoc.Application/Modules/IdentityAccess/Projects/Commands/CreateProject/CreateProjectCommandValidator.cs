using FluentValidation;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Project name is required.")
            .MaximumLength(100)
            .WithMessage("Project name cannot exceed 100 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithMessage("Project slug is required.")
            .MaximumLength(50)
            .WithMessage("Project slug cannot exceed 50 characters.")
            .Matches(@"^[a-z0-9-]+$")
            .WithMessage("Project slug can only contain lowercase alphanumeric characters and hyphens.");
    }
}
