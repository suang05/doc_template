namespace SmkDoc.Application.DTOs.Users;

/// <summary>
/// DTO representing user membership in a project.
/// </summary>
public record UserResultDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Command for inviting/adding a user to a project.
/// </summary>
public record InviteUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role = "Viewer"
);

/// <summary>
/// Command for updating a user's role in a project.
/// </summary>
public record UpdateUserRoleCommand(
    string Role
);
