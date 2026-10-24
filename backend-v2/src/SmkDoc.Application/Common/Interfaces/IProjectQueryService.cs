using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Cross-aggregate read query service for paginated project listings.
/// Executes optimized AsNoTracking database projections with direct DTO mapping.
/// </summary>
public interface IProjectQueryService
{
    Task<(IReadOnlyList<ProjectResultDto> Items, int TotalCount)> ListPagedByUserAsync(
        UserId userId,
        bool isSuperAdmin,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
