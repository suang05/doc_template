using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Cross-aggregate read query service for paginated template listings.
/// Executes optimized AsNoTracking database projections with direct DTO mapping.
/// Enforces multi-tenancy strictly via ambient EF Core global query filters.
/// </summary>
public interface ITemplateQueryService
{
    Task<(IReadOnlyList<TemplateResultDto> Items, int TotalCount)> ListPagedAsync(
        string? searchTerm,
        string? category,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct);
}
