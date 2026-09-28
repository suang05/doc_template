namespace SmkDoc.Application.UseCases.Users.Commands.RemoveUser;

public sealed record RemoveUserCommand(
    Guid ProjectId,
    Guid UserId,
    Guid CurrentUserId
);
