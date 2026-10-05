namespace SmkDoc.Api.Contracts.IdentityAccess.Users;

public record InviteUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role = "Viewer");

public record UpdateUserRoleRequest(string Role);

public record SetUserStatusRequest(bool IsActive);
