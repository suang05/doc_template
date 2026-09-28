using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;

namespace SmkDoc.Api.Models;

public record UserListItem(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt
) : UserResultDto(Id, Email, FirstName, LastName, Role, IsActive, CreatedAt);

public record InviteUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role = "Viewer"
);

public record UpdateUserRoleRequest(
    string Role
);
