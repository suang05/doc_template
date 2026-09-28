using FluentValidation;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;

public sealed class UpdateUserRoleCommandValidator : AbstractValidator<UpdateUserRoleCommand>
{
    private static readonly HashSet<string> ValidRoles = Enumeration.GetAll<RoleType>()
        .Select(r => r.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public UpdateUserRoleCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty()
            .WithMessage("ProjectId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required.")
            .Must(role => ValidRoles.Contains(role))
            .WithMessage("Invalid role. Allowed roles are: Admin, Developer, Viewer.");
    }
}
