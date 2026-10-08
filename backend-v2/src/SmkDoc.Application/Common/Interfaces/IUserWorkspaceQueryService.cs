using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Cross-aggregate read query service for resolving user workspace projects and workspace API keys.
/// Executes optimized database projections without entity hydration or N+1 roundtrips.
/// </summary>
public interface IUserWorkspaceQueryService
{
    /// <summary>
    /// Gets all active projects accessible to the specified user, including their resolved role.
    /// If user is SuperAdmin, returns all active projects with Admin role.
    /// </summary>
    Task<IReadOnlyList<AccessibleProjectDto>> GetAccessibleProjectsAsync(
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct = default);

    /// <summary>
    /// Gets active API keys for the specified workspace project.
    /// </summary>
    Task<IReadOnlyList<ApiKeyDto>> GetActiveApiKeysAsync(
        Guid projectId,
        CancellationToken ct = default);
}
