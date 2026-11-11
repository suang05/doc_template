using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

/// <summary>
/// Single-responsibility Query Use Case for listing templates within the active project context.
/// Executes via ITemplateQueryService with AsNoTracking and server-side paging.
/// </summary>
public sealed class ListTemplatesUseCase(
    ITemplateQueryService templateQueryService) : IUseCase<ListTemplatesQuery, TemplatePagedResultDto>
{
    public async Task<TemplatePagedResultDto> ExecuteAsync(ListTemplatesQuery query, CancellationToken ct = default)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await templateQueryService.ListPagedAsync(
            query.SearchTerm,
            query.Category,
            query.IsActive,
            page,
            pageSize,
            ct);

        return new TemplatePagedResultDto(items, totalCount, page, pageSize);
    }
}
