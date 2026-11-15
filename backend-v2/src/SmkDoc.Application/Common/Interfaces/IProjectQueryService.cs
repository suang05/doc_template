using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Cross-aggregate read query service for paginated project listings.
/// Executes optimized <c>AsNoTracking</c> projections respecting Zero Standing Access (ZSA)
/// for Data Plane portal queries vs. explicit Control Plane management queries.
/// </summary>
public interface IProjectQueryService
{
    /// <summary>
    /// Lists projects accessible to <paramref name="userId"/> via explicit <c>UserProjectRole</c>
    /// membership or an active, unrevoked <c>BreakGlassGrant</c> at <paramref name="now"/>.
    /// </summary>
    Task<(IReadOnlyList<ProjectResultDto> Items, long? TotalCount)> ListAccessiblePagedAsync(
        UserId userId,
        DateTimeOffset now,
        string? searchTerm,
        int page,
        int limit,
        bool includeTotal,
        CancellationToken ct);

    /// <summary>
    /// Lists all projects across the platform for Control Plane SuperAdmin management endpoints.
    /// </summary>
    Task<(IReadOnlyList<ProjectResultDto> Items, long? TotalCount)> ListAllControlPlanePagedAsync(
        string? searchTerm,
        int page,
        int limit,
        bool includeTotal,
        CancellationToken ct);
}
