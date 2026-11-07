namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;

public sealed record UpdateUserRoleCommand(
    Guid UserId,
    string Role,
    string? IfMatch = null
);
