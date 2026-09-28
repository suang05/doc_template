namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;

public sealed record SetUserStatusCommand(
    Guid ProjectId,
    Guid UserId,
    bool IsActive
);
