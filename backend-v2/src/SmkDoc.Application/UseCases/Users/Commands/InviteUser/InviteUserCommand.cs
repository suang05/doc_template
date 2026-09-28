namespace SmkDoc.Application.UseCases.Users.Commands.InviteUser;

public sealed record InviteUserCommand(
    Guid ProjectId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role = "Viewer"
);
