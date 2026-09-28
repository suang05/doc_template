namespace SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;

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
