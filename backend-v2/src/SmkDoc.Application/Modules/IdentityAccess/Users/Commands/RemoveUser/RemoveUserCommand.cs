namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;

public sealed record RemoveUserCommand(
    Guid ProjectId,
    Guid UserId,
    Guid CurrentUserId
);
