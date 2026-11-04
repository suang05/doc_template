namespace SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;

public sealed record InviteUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role = "Viewer"
);
