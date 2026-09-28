namespace SmkDoc.Application.UseCases.Users.Commands.SetUserStatus;

public sealed record SetUserStatusCommand(
    Guid ProjectId,
    Guid UserId,
    bool IsActive
);
