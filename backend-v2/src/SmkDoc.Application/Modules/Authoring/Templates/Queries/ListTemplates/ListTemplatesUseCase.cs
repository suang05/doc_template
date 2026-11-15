using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

/// <summary>
/// Single-responsibility Query Use Case for listing templates within the active project context.
/// Executes via ITemplateQueryService with AsNoTracking and server-side paging.
/// </summary>
public sealed class ListTemplatesUseCase(
    ITemplateQueryService templateQueryService) : IUseCase<ListTemplatesQuery, TemplatePagedResultDto>
{
    public async Task<TemplatePagedResultDto> ExecuteAsync(ListTemplatesQuery query, CancellationToken ct)
    {
        var (items, totalCount) = await templateQueryService.ListPagedAsync(
            query.SearchTerm,
            query.Category,
            query.IsActive,
            query.Page,
            query.Limit,
            query.IncludeTotal,
            ct);

        return new TemplatePagedResultDto(items, totalCount, query.Page, query.Limit);
    }
}
