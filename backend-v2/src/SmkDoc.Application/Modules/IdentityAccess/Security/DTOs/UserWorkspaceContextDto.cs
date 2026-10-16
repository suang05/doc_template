namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Resolved workspace context for an authenticated user session.
/// </summary>
public record UserWorkspaceContextDto(
    Guid? ActiveProjectId,
    string ActiveRole,
    Guid? DefaultProjectId,
    IReadOnlyList<AccessibleProjectDto> AccessibleProjects
);
