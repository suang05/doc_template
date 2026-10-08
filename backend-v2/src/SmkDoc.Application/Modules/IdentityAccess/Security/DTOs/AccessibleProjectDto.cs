namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Project access information for an authenticated user.
/// </summary>
public record AccessibleProjectDto(
    Guid Id,
    string Name,
    string Slug,
    string Role
);
