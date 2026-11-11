namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Resolved workspace context for an authenticated user session.
/// </summary>
public sealed record UserWorkspaceContextResultDto(
    Guid? ActiveProjectId,
    string ActiveRole,
    Guid? DefaultProjectId,
    IReadOnlyList<AccessibleProjectResultDto> AccessibleProjects
);

