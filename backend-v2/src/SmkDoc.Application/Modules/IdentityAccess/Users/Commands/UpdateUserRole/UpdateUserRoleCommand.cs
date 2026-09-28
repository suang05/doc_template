namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;

public sealed record UpdateUserRoleCommand(
    Guid ProjectId,
    Guid UserId,
    string Role
);
