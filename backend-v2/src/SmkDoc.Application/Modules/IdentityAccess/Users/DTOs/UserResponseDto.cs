namespace SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;

/// <summary>
/// Immutable response DTO representing user membership in a project.
/// </summary>
public sealed record UserResponseDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt
);
