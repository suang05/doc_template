using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Cross-aggregate read query service for paginated template listings.
/// Executes optimized AsNoTracking database projections with direct DTO mapping.
/// </summary>
public interface ITemplateQueryService
{
    Task<(IReadOnlyList<TemplateResultDto> Items, int TotalCount)> ListPagedAsync(
        ProjectId projectId,
        string? searchTerm,
        string? category,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
