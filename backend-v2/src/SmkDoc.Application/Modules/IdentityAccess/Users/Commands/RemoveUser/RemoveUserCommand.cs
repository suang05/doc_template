namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;

public sealed record RemoveUserCommand(
    Guid UserId,
    Guid CurrentUserId,
    string? IfMatch = null
);
