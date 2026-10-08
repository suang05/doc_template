namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Profile result returned by GET /api/v1/auth/me for current authenticated user.
/// </summary>
public record CurrentUserProfileResultDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string SystemRole,
    IReadOnlyList<AccessibleProjectDto> AccessibleProjects,
    Guid? DefaultProjectId
);
