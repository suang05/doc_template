namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// User profile data embedded in login tokens and responses.
/// </summary>
public record UserProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string SystemRole
);
