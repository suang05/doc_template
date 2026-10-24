using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

/// <summary>
/// Single-responsibility Query Use Case for listing templates.
/// Uses ambient ICurrentProjectContext when available with query fallback.
/// Executes via ITemplateQueryService with AsNoTracking and server-side paging.
/// </summary>
public sealed class ListTemplatesUseCase(
    ITemplateQueryService templateQueryService,
    ICurrentProjectContext? projectContext = null) : IUseCase<ListTemplatesQuery, TemplatePagedResultDto>
{
    private readonly ITemplateQueryService _templateQueryService = templateQueryService;
    private readonly ICurrentProjectContext? _projectContext = projectContext;

    public async Task<TemplatePagedResultDto> ExecuteAsync(ListTemplatesQuery query, CancellationToken ct = default)
    {
        var activeProjectId = query.ProjectId != Guid.Empty
            ? query.ProjectId
            : (_projectContext?.IsResolved == true ? _projectContext.ProjectId.Value : Guid.Empty);

        if (activeProjectId == Guid.Empty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        var (items, totalCount) = await _templateQueryService.ListPagedAsync(
            new ProjectId(activeProjectId),
            query.SearchTerm,
            query.Category,
            query.IsActive,
            query.Page,
            query.PageSize,
            ct);

        return new TemplatePagedResultDto(items, totalCount, query.Page, query.PageSize);
    }
}
