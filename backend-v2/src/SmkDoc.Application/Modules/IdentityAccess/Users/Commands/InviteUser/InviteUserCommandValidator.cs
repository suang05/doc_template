using FluentValidation;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;

public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    private static readonly HashSet<string> ValidRoles = Enumeration.GetAll<RoleType>()
        .Select(r => r.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public InviteUserCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty()
            .WithMessage("ProjectId is required.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("A valid email address is required.");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required.")
            .Must(role => ValidRoles.Contains(role))
            .WithMessage("Invalid role. Allowed roles are: Admin, Developer, Viewer.");
    }
}
