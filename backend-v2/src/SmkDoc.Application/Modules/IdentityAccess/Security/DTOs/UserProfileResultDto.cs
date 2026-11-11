namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Result DTO representing the identity and profile information of an authenticated user.
/// </summary>
public sealed record UserProfileResultDto(
    Guid Id,
    string Email,
    string FirstName,
    string? LastName,
    string SystemRole
);

