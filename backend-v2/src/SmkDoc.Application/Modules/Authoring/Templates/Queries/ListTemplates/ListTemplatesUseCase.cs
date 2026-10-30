using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

/// <summary>
/// Single-responsibility Query Use Case for listing templates within the active project context.
/// Executes via ITemplateQueryService with AsNoTracking and server-side paging.
/// </summary>
public sealed class ListTemplatesUseCase(
    ITemplateQueryService templateQueryService,
    ICurrentProjectContext projectContext) : IUseCase<ListTemplatesQuery, TemplatePagedResultDto>
{
    public async Task<TemplatePagedResultDto> ExecuteAsync(ListTemplatesQuery query, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Active project context is required to list templates.");
        }

        var (items, totalCount) = await templateQueryService.ListPagedAsync(
            projectContext.ProjectId,
            query.SearchTerm,
            query.Category,
            query.IsActive,
            query.Page,
            query.PageSize,
            ct);

        return new TemplatePagedResultDto(items, totalCount, query.Page, query.PageSize);
    }
}
