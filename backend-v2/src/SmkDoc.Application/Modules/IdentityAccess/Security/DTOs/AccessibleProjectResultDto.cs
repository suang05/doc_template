namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Result DTO representing a project accessible to an authenticated user, including their assigned role.
/// </summary>
public sealed record AccessibleProjectResultDto(
    Guid Id,
    string Name,
    string Slug,
    string Role
);

