namespace SmkDoc.Application.UseCases.Users.Commands.UpdateUserRole;

public sealed record UpdateUserRoleCommand(
    Guid ProjectId,
    Guid UserId,
    string Role
);
